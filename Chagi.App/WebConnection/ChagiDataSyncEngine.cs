using System.Net;
using static ITF_Taekwondo_Gradering.WebConnection.DataIntegrity;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ITF_Taekwondo_Gradering.WebConnection;

public sealed class ChagiDataSyncEngine(HttpClient client, Uri baseUri, string appDataDirectory, Func<DateTime>? utcNow = null)
{
    public static readonly TimeSpan AutomaticInterval = TimeSpan.FromMinutes(15);
    const string MetadataName = ".chagi-manifest.json";
    static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    readonly SemaphoreSlim _sync = new(1, 1);
    readonly Func<DateTime> _utcNow = utcNow ?? (() => DateTime.UtcNow);
    DateTime? _lastAttempt;
    string DataRoot => Path.Combine(appDataDirectory, "Data");
    string StatePath => Path.Combine(DataRoot, "chagi-sync-state.json");

    public async Task<ChagiDataSyncResult> SyncAsync(ChagiDataSyncTrigger trigger, CancellationToken token = default)
    {
        await _sync.WaitAsync(token).ConfigureAwait(false);
        string? staging = null;
        var state = new SyncState();
        try
        {
            state = await ReadJsonAsync<SyncState>(StatePath).ConfigureAwait(false) ?? new();
            var now = _utcNow();
            var last = _lastAttempt ?? state.LastAttemptUtc;
            if (trigger == ChagiDataSyncTrigger.Automatic && last.HasValue && last <= now && now - last.Value < AutomaticInterval)
                return Result(ChagiDataSyncOutcome.Skipped, "Nästa automatiska kontroll sker senare.", state);

            token.ThrowIfCancellationRequested();
            _lastAttempt = state.LastAttemptUtc = now;
            await SaveStateAsync(state).ConfigureAwait(false);
            var current = SyncedDataStore.GetDirectory(appDataDirectory);
            var previous = await ReadJsonAsync<SnapshotMetadata>(Path.Combine(current, MetadataName)).ConfigureAwait(false);
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(baseUri, "manifest.json"));
            request.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true };
            if (previous?.Manifest != null && EntityTagHeaderValue.TryParse(previous.ETag, out var tag))
                request.Headers.IfNoneMatch.Add(tag);
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
            ChagiDataManifest manifest;
            if (response.StatusCode == HttpStatusCode.NotModified)
                manifest = previous?.Manifest ?? throw new InvalidDataException("Manifest saknas lokalt.");
            else
            {
                response.EnsureSuccessStatusCode();
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
                deadline.CancelAfter(TimeSpan.FromSeconds(20));
                await using var stream = await response.Content.ReadAsStreamAsync(deadline.Token).ConfigureAwait(false);
                manifest = await JsonSerializer.DeserializeAsync<ChagiDataManifest>(stream, JsonOptions, deadline.Token).ConfigureAwait(false)
                    ?? throw new InvalidDataException("Manifest saknas.");
            }

            var files = ValidateManifest(manifest);
            var previousHashes = previous?.Manifest?.Files
                .GroupBy(file => file.Path, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First().Sha256, StringComparer.OrdinalIgnoreCase) ?? new();
            var lengths = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            var downloads = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in files)
            {
                token.ThrowIfCancellationRequested();
                var path = LocalPath(current, file.Path);
                if (!File.Exists(path)) { downloads.Add(file.Path); continue; }
                var length = new FileInfo(path).Length;
                var trusted = trigger == ChagiDataSyncTrigger.Automatic &&
                    previousHashes.TryGetValue(file.Path, out var hash) && string.Equals(hash, file.Sha256, StringComparison.OrdinalIgnoreCase) &&
                    previous!.FileLengths.TryGetValue(file.Path, out var savedLength) && length == savedLength;
                if (!trusted)
                {
                    trusted = MatchesChecksum(await File.ReadAllBytesAsync(path, token).ConfigureAwait(false), file);
                }
                if (trusted) lengths[file.Path] = length;
                else downloads.Add(file.Path);
            }

            var expected = files.Select(file => file.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var removed = Directory.Exists(current) && Directory.EnumerateFiles(current, "*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(current, path).Replace('\\', '/'))
                .Any(path => path != MetadataName && !expected.Contains(path));
            var metadata = new SnapshotMetadata
            {
                Manifest = manifest,
                ETag = response.StatusCode == HttpStatusCode.NotModified ? previous?.ETag : response.Headers.ETag?.ToString(),
                FileLengths = lengths
            };
            if (downloads.Count == 0 && !removed)
            {
                if (response.StatusCode != HttpStatusCode.NotModified || previous == null)
                    await WriteJsonAtomicallyAsync(Path.Combine(current, MetadataName), metadata, token).ConfigureAwait(false);
                state.LastSuccessfulSyncUtc = _utcNow();
                state.LastOutcome = ChagiDataSyncOutcome.NoChange;
                state.LastMessage = "Inga nya resultat att hämta.";
                await SaveStateAsync(state).ConfigureAwait(false);
                return Result(state.LastOutcome.Value, state.LastMessage, state, response.StatusCode == HttpStatusCode.NotModified);
            }

            var generation = Guid.NewGuid().ToString("N");
            staging = Path.Combine(DataRoot, "synced-generations", generation);
            Directory.CreateDirectory(staging);
            long bytesDownloaded = 0;
            foreach (var file in files)
            {
                token.ThrowIfCancellationRequested();
                var target = LocalPath(staging, file.Path);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                if (downloads.Contains(file.Path))
                {
                    var escaped = string.Join('/', file.Path.Split('/').Select(Uri.EscapeDataString));
                    using var fileRequest = new HttpRequestMessage(HttpMethod.Get, new Uri(baseUri, escaped));
                    fileRequest.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true };
                    using var fileResponse = await client.SendAsync(fileRequest, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
                    fileResponse.EnsureSuccessStatusCode();
                    using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
                    deadline.CancelAfter(TimeSpan.FromSeconds(20));
                    var bytes = await fileResponse.Content.ReadAsByteArrayAsync(deadline.Token).ConfigureAwait(false);
                    if (!MatchesChecksum(bytes, file))
                        throw new InvalidDataException($"Felaktig checksumma för {file.Path}.");
                    await File.WriteAllBytesAsync(target, bytes, token).ConfigureAwait(false);
                    lengths[file.Path] = bytes.LongLength;
                    bytesDownloaded += bytes.LongLength;
                }
                else File.Copy(LocalPath(current, file.Path), target);
            }
            await WriteJsonAtomicallyAsync(Path.Combine(staging, MetadataName), metadata, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            // Readers retain the previous immutable directory until their operation finishes.
            // One atomic pointer replacement publishes the complete new set of files.
            var pointer = Path.Combine(DataRoot, "synced-current.txt");
            await File.WriteAllTextAsync(pointer + ".tmp", generation, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            File.Move(pointer + ".tmp", pointer, true);
            staging = null;
            state.LastSuccessfulSyncUtc = _utcNow();
            state.LastOutcome = ChagiDataSyncOutcome.Updated;
            state.LastMessage = "Synkning klar. Nya resultat hämtades.";
            await SaveStateAsync(state).ConfigureAwait(false);
            return new()
            {
                Outcome = ChagiDataSyncOutcome.Updated, Message = state.LastMessage,
                AttemptedAtUtc = state.LastAttemptUtc!.Value, LastSuccessfulSyncUtc = state.LastSuccessfulSyncUtc,
                FilesDownloaded = downloads.Count, BytesDownloaded = bytesDownloaded,
                ManifestNotModified = response.StatusCode == HttpStatusCode.NotModified
            };
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
            state.LastOutcome = ChagiDataSyncOutcome.Failed;
            state.LastMessage = "Synkning misslyckades. Befintlig data finns kvar.";
            await SaveStateAsync(state).ConfigureAwait(false);
            return Result(state.LastOutcome.Value, state.LastMessage, state);
        }
        finally
        {
            if (staging != null)
            {
                try { Directory.Delete(staging, true); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            _sync.Release();
        }
    }

    // Called once before the first app window is created, when no previous process has readers.
    public void PruneOldSnapshots()
    {
        var root = Path.Combine(DataRoot, "synced-generations");
        var current = SyncedDataStore.GetDirectory(appDataDirectory);
        if (!Directory.Exists(root)) return;
        foreach (var directory in Directory.EnumerateDirectories(root))
            if (directory != current && Guid.TryParseExact(Path.GetFileName(directory), "N", out _))
                try { Directory.Delete(directory, true); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
    }

    public async Task<ChagiDataSyncStatus> GetStatusAsync()
    {
        var state = await ReadJsonAsync<SyncState>(StatePath).ConfigureAwait(false) ?? new();
        return new() { LastAttemptUtc = state.LastAttemptUtc, LastSuccessfulSyncUtc = state.LastSuccessfulSyncUtc, LastOutcome = state.LastOutcome, LastMessage = state.LastMessage };
    }

    static string LocalPath(string root, string relative) => Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));

    static ChagiDataSyncResult Result(ChagiDataSyncOutcome outcome, string message, SyncState state, bool manifestNotModified = false) => new()
    {
        Outcome = outcome, Message = message, AttemptedAtUtc = state.LastAttemptUtc ?? DateTime.UtcNow,
        LastSuccessfulSyncUtc = state.LastSuccessfulSyncUtc, ManifestNotModified = manifestNotModified
    };
    async Task SaveStateAsync(SyncState state)
    {
        try { await WriteJsonAtomicallyAsync(StatePath, state, CancellationToken.None).ConfigureAwait(false); }
        catch (IOException ex) { System.Diagnostics.Debug.WriteLine(ex); }
        catch (UnauthorizedAccessException ex) { System.Diagnostics.Debug.WriteLine(ex); }
    }
    static async Task<T?> ReadJsonAsync<T>(string path)
    {
        try { return JsonSerializer.Deserialize<T>(await File.ReadAllTextAsync(path).ConfigureAwait(false), JsonOptions); }
        catch (IOException) { return default; }
        catch (UnauthorizedAccessException) { return default; }
        catch (JsonException) { return default; }
    }
    static async Task WriteJsonAtomicallyAsync<T>(string path, T value, CancellationToken token)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path + ".tmp", JsonSerializer.Serialize(value, JsonOptions), token).ConfigureAwait(false);
        token.ThrowIfCancellationRequested();
        File.Move(path + ".tmp", path, true);
    }
    sealed class SnapshotMetadata
    {
        public ChagiDataManifest? Manifest { get; set; }
        public string? ETag { get; set; }
        public Dictionary<string, long> FileLengths { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
    sealed class SyncState
    {
        public DateTime? LastAttemptUtc { get; set; }
        public DateTime? LastSuccessfulSyncUtc { get; set; }
        public ChagiDataSyncOutcome? LastOutcome { get; set; }
        public string LastMessage { get; set; } = string.Empty;
    }
}
