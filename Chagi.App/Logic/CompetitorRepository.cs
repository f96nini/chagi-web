using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using ITF_Taekwondo_Gradering.Models;
using ITF_Taekwondo_Gradering.Models.Ranking;
using ITF_Taekwondo_Gradering.WebConnection;

namespace ITF_Taekwondo_Gradering.Logic
{
    public static class CompetitorRepository
    {
        static List<Competitor>? _cache;
        static readonly SemaphoreSlim InitializeLock = new(1, 1);
        static readonly string StorageFileName = "competitors.v11.json";
        static readonly string StorageMetadataFileName = "competitors.v11.metadata.json";
        static readonly string FavoritesStorageKey = "favorite_competitors.v1";
        static readonly string CacheBuildVersion = "2026-10-17-specific-belt-v11";
        static HashSet<string>? _favoriteKeys;
        static int _changeVersion;
        static readonly JsonSerializerOptions CacheJsonOptions = CreateCacheJsonOptions();

        public static Task InitializeAsync()
            => Task.Run(() => InitializeCoreAsync());

        static async Task InitializeCoreAsync()
        {
            if (_cache != null)
                return;

            await InitializeLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_cache != null)
                    return;

                var storagePath = Path.Combine(FileSystem.AppDataDirectory, StorageFileName);
                var storageMetadataPath = Path.Combine(FileSystem.AppDataDirectory, StorageMetadataFileName);
                var snapshotDirectory = SyncedDataStore.GetDirectory(FileSystem.AppDataDirectory);
                var sourceFiles = await LoadSourceFilesAsync(snapshotDirectory).ConfigureAwait(false);
                var matchHistory = await CompetitionMatchService.GetHistoryAsync(snapshotDirectory).ConfigureAwait(false);
                var sourceFingerprint = ComputeSourceFingerprint(sourceFiles.Append(
                    ("matches-history.json", JsonSerializer.Serialize(matchHistory))));

                if (File.Exists(storagePath) &&
                    IsCacheCurrent(storageMetadataPath, sourceFingerprint))
                {
                    try
                    {
                        var json = await File.ReadAllTextAsync(storagePath).ConfigureAwait(false);
                        var cachedCompetitors = JsonSerializer.Deserialize<List<Competitor>>(json, CacheJsonOptions)
                            ?? throw new JsonException("Missing cached results.");
                        var cacheWasNormalized = CompetitorResultsParser.NormalizeCache(cachedCompetitors);
                        var rankingsWereRebuilt = CompetitorResultsParser.RebuildRankingSnapshots(cachedCompetitors);
                        ApplyFavorites(cachedCompetitors);
                        _cache = cachedCompetitors;
                        if (cacheWasNormalized || rankingsWereRebuilt)
                            await SaveCacheAsync(storagePath, storageMetadataPath, sourceFingerprint).ConfigureAwait(false);
                        return;
                    }
                    catch (Exception ex)
                    {
                        // A broken cache must not hide the source results.
                        System.Diagnostics.Debug.WriteLine(ex);
                        DeletePersistentCacheFiles(storagePath, storageMetadataPath);
                    }
                }

                var parsedCompetitors = CompetitorResultsParser.Parse(sourceFiles, matchHistory);

                ApplyFavorites(parsedCompetitors);
                CompetitorResultsParser.RebuildRankingSnapshots(parsedCompetitors);
                _cache = parsedCompetitors;

                await SaveCacheAsync(storagePath, storageMetadataPath, sourceFingerprint).ConfigureAwait(false);
            }
            catch
            {
                // swallow to not crash startup; leave _cache empty list
                _cache ??= new List<Competitor>();
            }
            finally
            {
                InitializeLock.Release();
            }
        }

        static bool IsCacheCurrent(string metadataPath, string sourceFingerprint)
        {
            try
            {
                if (!File.Exists(metadataPath))
                    return false;

                var json = File.ReadAllText(metadataPath);
                var metadata = JsonSerializer.Deserialize<CompetitorCacheMetadata>(json);
                return metadata != null &&
                       string.Equals(metadata.SourceFingerprint, sourceFingerprint, StringComparison.Ordinal);
            }
            catch
            {
                return false;
            }
        }

        static string ComputeSourceFingerprint(IEnumerable<(string fileName, string text)> sourceFiles)
        {
            var files = sourceFiles
                .OrderBy(file => file.fileName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (files.Count == 0)
                return "missing";

            using var sha = SHA256.Create();
            using var stream = new MemoryStream();
            var versionBytes = Encoding.UTF8.GetBytes($"{CacheBuildVersion}\n");
            stream.Write(versionBytes, 0, versionBytes.Length);

            foreach (var file in files)
            {
                var normalizedName = file.fileName.Replace('\\', '/');
                var contentBytes = Encoding.UTF8.GetBytes(file.text);
                var header = $"{normalizedName}|{contentBytes.Length}\n";
                var headerBytes = Encoding.UTF8.GetBytes(header);
                stream.Write(headerBytes, 0, headerBytes.Length);
                stream.Write(contentBytes, 0, contentBytes.Length);
                stream.WriteByte((byte)'\n');
            }

            stream.Position = 0;
            var hash = sha.ComputeHash(stream);
            return Convert.ToHexString(hash);
        }

        static async Task<List<(string fileName, string text)>> LoadSourceFilesAsync(string syncedAppDataDir)
        {
            var packageDir = AppContext.BaseDirectory ?? string.Empty;

            if (Directory.Exists(syncedAppDataDir))
            {
                var syncedFiles = await LoadSourceFilesFromDirectoryAsync(syncedAppDataDir).ConfigureAwait(false);
                if (syncedFiles.Count > 0)
                    return syncedFiles;
            }

            var developmentDataDir = FindDevelopmentDataDirectory(packageDir);
            if (!string.IsNullOrWhiteSpace(developmentDataDir))
                return await LoadSourceFilesFromDirectoryAsync(developmentDataDir).ConfigureAwait(false);

            if (OperatingSystem.IsAndroid())
            {
                var packageAssetFiles = await LoadSourceFilesFromPackageAssetsAsync().ConfigureAwait(false);
                if (packageAssetFiles.Count > 0)
                    return packageAssetFiles;
            }

            var packageDataDir = Path.Combine(packageDir, "Data");
            if (Directory.Exists(packageDataDir))
                return await LoadSourceFilesFromDirectoryAsync(packageDataDir).ConfigureAwait(false);

            var packageAssetFilesFallback = await LoadSourceFilesFromPackageAssetsAsync().ConfigureAwait(false);
            if (packageAssetFilesFallback.Count > 0)
                return packageAssetFilesFallback;

            var legacyAppDataDir = Path.Combine(FileSystem.AppDataDirectory, "Data");
            if (!OperatingSystem.IsAndroid() && Directory.Exists(legacyAppDataDir))
                return await LoadSourceFilesFromDirectoryAsync(legacyAppDataDir).ConfigureAwait(false);

            return new List<(string fileName, string text)>();
        }

        static string? FindDevelopmentDataDirectory(string startDirectory)
        {
            if (string.IsNullOrWhiteSpace(startDirectory))
                return null;

            var current = new DirectoryInfo(startDirectory);
            while (current != null)
            {
                var projectFile = Path.Combine(current.FullName, "ITF-Taekwondo-Gradering.csproj");
                var dataDir = Path.Combine(current.FullName, "Data");
                if (File.Exists(projectFile) && Directory.Exists(dataDir))
                    return dataDir;

                current = current.Parent;
            }

            return null;
        }

        static async Task<List<(string fileName, string text)>> LoadSourceFilesFromDirectoryAsync(string directoryPath)
        {
            var files = Directory.GetFiles(directoryPath, "*.txt", SearchOption.AllDirectories)
                .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}archive{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) &&
                               !path.Contains($"{Path.AltDirectorySeparatorChar}archive{Path.AltDirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var result = new List<(string fileName, string text)>(files.Count);
            foreach (var file in files)
            {
                var text = await File.ReadAllTextAsync(file).ConfigureAwait(false);
                var relativePath = Path.GetRelativePath(directoryPath, file).Replace('\\', '/');
                result.Add((relativePath, text));
            }

            return result;
        }

        static async Task<List<(string fileName, string text)>> LoadSourceFilesFromPackageAssetsAsync()
        {
            const string fileListAssetPath = "Data/filelist.txt";

            try
            {
                await using var fileListStream = await FileSystem.OpenAppPackageFileAsync(fileListAssetPath).ConfigureAwait(false);
                using var reader = new StreamReader(fileListStream);
                var fileListContent = await reader.ReadToEndAsync().ConfigureAwait(false);

                var assetPaths = fileListContent
                    .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(path => path.Trim().Replace('\\', '/'))
                    .Where(path => !string.IsNullOrWhiteSpace(path) &&
                                   !path.StartsWith("archive/", StringComparison.OrdinalIgnoreCase))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var result = new List<(string fileName, string text)>(assetPaths.Count);
                foreach (var assetPath in assetPaths)
                {
                    await using var assetStream = await FileSystem.OpenAppPackageFileAsync($"Data/{assetPath}").ConfigureAwait(false);
                    using var assetReader = new StreamReader(assetStream);
                    var text = await assetReader.ReadToEndAsync().ConfigureAwait(false);
                    result.Add((assetPath, text));
                }

                return result;
            }
            catch
            {
                return new List<(string fileName, string text)>();
            }
        }

        static async Task SaveCacheAsync(string storagePath, string storageMetadataPath, string sourceFingerprint)
        {
            try
            {
                var json = JsonSerializer.Serialize(_cache ?? new List<Competitor>(), CacheJsonOptions);
                Directory.CreateDirectory(Path.GetDirectoryName(storagePath) ?? FileSystem.AppDataDirectory);
                // Publish complete files only; metadata is committed last.
                await File.WriteAllTextAsync(storagePath + ".tmp", json).ConfigureAwait(false);
                File.Move(storagePath + ".tmp", storagePath, overwrite: true);

                var metadata = new CompetitorCacheMetadata
                {
                    SourceFingerprint = sourceFingerprint,
                    CachedAtUtc = DateTime.UtcNow
                };
                var metadataJson = JsonSerializer.Serialize(metadata, CacheJsonOptions);
                await File.WriteAllTextAsync(storageMetadataPath + ".tmp", metadataJson).ConfigureAwait(false);
                File.Move(storageMetadataPath + ".tmp", storageMetadataPath, overwrite: true);
            }
            catch
            {
                // ignore
            }
        }

        static void DeletePersistentCacheFiles(string storagePath, string storageMetadataPath)
        {
            try
            {
                if (File.Exists(storagePath))
                    File.Delete(storagePath);

                if (File.Exists(storageMetadataPath))
                    File.Delete(storageMetadataPath);
            }
            catch
            {
                // ignore
            }
        }

        public static async Task<List<Competitor>> GetAllAsync()
        {
            if (_cache == null)
            {
                await InitializeAsync().ConfigureAwait(false);
            }

            return (_cache ?? new List<Competitor>()).ToList();
        }

        public static bool IsFavorite(Competitor competitor)
        {
            return GetFavoriteKeys().Contains(competitor.FavoriteKey);
        }

        public static async Task SetFavoriteAsync(Competitor competitor, bool isFavorite)
        {
            var favoriteKeys = GetFavoriteKeys();

            if (isFavorite)
                favoriteKeys.Add(competitor.FavoriteKey);
            else
                favoriteKeys.Remove(competitor.FavoriteKey);

            competitor.IsFavorite = isFavorite;

            if (_cache != null)
            {
                foreach (var cachedCompetitor in _cache.Where(c => c.FavoriteKey == competitor.FavoriteKey))
                {
                    cachedCompetitor.IsFavorite = isFavorite;
                }
            }

            _changeVersion++;
            SaveFavoriteKeys(favoriteKeys);
            await Task.CompletedTask;
        }

        public static Task ClearCacheAsync()
            => Task.Run(() => ClearCacheCoreAsync());

        static async Task ClearCacheCoreAsync()
        {
            await InitializeLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var storagePath = Path.Combine(FileSystem.AppDataDirectory, StorageFileName);
                var storageMetadataPath = Path.Combine(FileSystem.AppDataDirectory, StorageMetadataFileName);
                DeletePersistentCacheFiles(storagePath, storageMetadataPath);

                var dataDir = Path.Combine(FileSystem.AppDataDirectory, "Data");
                if (Directory.Exists(dataDir))
                {
                    try
                    {
                        Directory.Delete(dataDir, true);
                    }
                    catch
                    {
                        // ignore
                    }
                }

                _cache = null;
                _changeVersion++;
            }
            finally
            {
                InitializeLock.Release();
            }

            await InitializeAsync().ConfigureAwait(false);
        }

        public static Task ReloadFromSourceAsync()
            => Task.Run(() => ReloadFromSourceCoreAsync());

        static async Task ReloadFromSourceCoreAsync()
        {
            await InitializeLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var storagePath = Path.Combine(FileSystem.AppDataDirectory, StorageFileName);
                var storageMetadataPath = Path.Combine(FileSystem.AppDataDirectory, StorageMetadataFileName);
                DeletePersistentCacheFiles(storagePath, storageMetadataPath);
                _cache = null;
                _changeVersion++;
            }
            finally
            {
                InitializeLock.Release();
            }

            await InitializeAsync().ConfigureAwait(false);
        }

        static void ApplyFavorites(IEnumerable<Competitor> competitors)
        {
            var favoriteKeys = GetFavoriteKeys();

            foreach (var competitor in competitors)
            {
                competitor.IsFavorite = favoriteKeys.Contains(competitor.FavoriteKey);
            }
        }

        static HashSet<string> GetFavoriteKeys()
        {
            if (_favoriteKeys != null)
                return _favoriteKeys;

            try
            {
                var raw = Preferences.Get(FavoritesStorageKey, string.Empty);
                var keys = string.IsNullOrWhiteSpace(raw)
                    ? new List<string>()
                    : JsonSerializer.Deserialize<List<string>>(raw) ?? new List<string>();

                _favoriteKeys = new HashSet<string>(keys.Select(key =>
                {
                    var parts = key.Split('|', 2);
                    return parts.Length == 2
                        ? $"{parts[0].Trim()}|{ClubCatalogService.NormalizeClubName(parts[1])}".ToLowerInvariant()
                        : key;
                }), StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                _favoriteKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            return _favoriteKeys;
        }

        static void SaveFavoriteKeys(HashSet<string> favoriteKeys)
        {
            try
            {
                Preferences.Set(FavoritesStorageKey, JsonSerializer.Serialize(favoriteKeys.OrderBy(x => x).ToList()));
            }
            catch
            {
                // ignore
            }
        }

        static JsonSerializerOptions CreateCacheJsonOptions()
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = false
            };
            options.Converters.Add(new ChildBeltJsonConverter());
            return options;
        }

        public static int GetChangeVersion()
        {
            return _changeVersion;
        }
    }

    sealed class CompetitorCacheMetadata
    {
        public string SourceFingerprint { get; init; } = string.Empty;
        public DateTime CachedAtUtc { get; init; }
    }

    sealed class ChildBeltJsonConverter : JsonConverter<ChildBelt>
    {
        public override ChildBelt Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
                return ChildBelt.Kup10_1;

            using var document = JsonDocument.ParseValue(ref reader);
            var root = document.RootElement;

            if (root.ValueKind == JsonValueKind.String)
            {
                var beltName = root.GetString();
                return ResolveChildBelt(beltName, null);
            }

            if (root.ValueKind == JsonValueKind.Object)
            {
                string? beltName = null;
                int? beltLevel = null;

                if (root.TryGetProperty("Name", out var nameProperty) && nameProperty.ValueKind == JsonValueKind.String)
                {
                    beltName = nameProperty.GetString();
                }

                if (root.TryGetProperty("Level", out var levelProperty) && levelProperty.TryGetInt32(out var parsedLevel))
                {
                    beltLevel = parsedLevel;
                }

                return ResolveChildBelt(beltName, beltLevel);
            }

            return ChildBelt.Kup10_1;
        }

        public override void Write(Utf8JsonWriter writer, ChildBelt value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteString("Name", value.Name);
            writer.WriteNumber("Level", value.Level);
            writer.WriteEndObject();
        }

        static ChildBelt ResolveChildBelt(string? beltName, int? beltLevel)
        {
            if (!string.IsNullOrWhiteSpace(beltName))
            {
                var byName = ChildBelt.GetBeltByName(beltName);
                if (byName != null)
                    return byName;
            }

            if (beltLevel.HasValue)
            {
                var byLevel = ChildBelt.GetBeltByLevel(beltLevel.Value);
                if (byLevel != null)
                    return byLevel;
            }

            return ChildBelt.Kup10_1;
        }
    }
}
