using System.Text;
using System.Text.Json;
using ITF_Taekwondo_Gradering.Logic;
using ITF_Taekwondo_Gradering.Models.Ranking;
using ITF_Taekwondo_Gradering.WebConnection;

namespace Chagi.Core.Data;

public interface IDataSnapshotStore
{
    Task<PublishedSnapshot?> ReadAsync(CancellationToken token = default);
    Task WriteAsync(PublishedSnapshot snapshot, CancellationToken token = default);
}

public sealed class PublishedSnapshot
{
    public string Origin { get; set; } = "";
    public ChagiDataManifest Manifest { get; set; } = new();
    public Dictionary<string, byte[]> Files { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public DateTime CheckedAtUtc { get; set; }
}

public sealed record ResultsData(IReadOnlyList<Competitor> Fighters,
    IReadOnlyList<CompetitionCatalogEntry> Competitions, IReadOnlyList<CompetitionMatchHistory> MatchHistory, string? Version,
    DateTime CheckedAtUtc, bool IsStale, bool LatestOnly, string? Notice);

// Browser HTTP caching performs ETag/Last-Modified revalidation without custom
// cross-origin request headers. The manifest/hash contract is shared with MAUI.
public sealed class PublishedResultsClient(HttpClient http, Uri baseUri, IDataSnapshotStore store,
    Func<DateTime>? utcNow = null)
{
    public static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(15);
    readonly SemaphoreSlim gate = new(1, 1);
    readonly Func<DateTime> now = utcNow ?? (() => DateTime.UtcNow);
    PublishedSnapshot? current;
    bool loaded;

    public async Task<ResultsData> LoadAsync(bool latestOnly = false, bool refresh = false,
        CancellationToken token = default)
    {
        await gate.WaitAsync(token);
        try
        {
            if (!loaded)
            {
                try { current = await store.ReadAsync(token); }
                catch (Exception ex) when (ex is not OperationCanceledException) { current = null; }
                if (current?.Origin != baseUri.AbsoluteUri) current = null;
                loaded = true;
            }

            var next = new PublishedSnapshot { Origin = baseUri.AbsoluteUri };
            string? notice = null;
            try
            {
                var due = refresh || current == null || now() - current.CheckedAtUtc >= RefreshInterval || current.CheckedAtUtc > now();
                next.Manifest = due
                    ? JsonSerializer.Deserialize<ChagiDataManifest>(await http.GetStringAsync(new Uri(baseUri, "manifest.json"), token))
                        ?? throw new InvalidDataException("Manifest saknas.")
                    : current!.Manifest;
                var entries = DataIntegrity.ValidateManifest(next.Manifest);
                var selected = SelectResults(entries, latestOnly);
                // Retain only files that still belong to this manifest and pass verification.
                foreach (var file in entries)
                    if (current?.Files.TryGetValue(file.Path, out var saved) == true && DataIntegrity.MatchesChecksum(saved, file))
                        next.Files[file.Path] = saved;
                foreach (var file in selected)
                    await DownloadAsync(next, file, token);
                var indexEntry = entries.SingleOrDefault(f => f.Path == "matches/index.json");
                if (indexEntry != null)
                {
                    await DownloadAsync(next, indexEntry, token);
                    var latestDates = selected.Select(f => f.Path["results/".Length..].Split('_')[0]).ToHashSet();
                    foreach (var entry in CompetitionMatchParser.ParseIndex(Text(next.Files[indexEntry.Path]))
                        .Where(e => e.CompetitionDate.Date <= now().ToLocalTime().Date && (!latestOnly || latestDates.Contains(e.CompetitionDate.ToString("yyyy-MM-dd")))))
                    {
                        var file = entries.SingleOrDefault(f => f.Path == "matches/" + entry.File)
                            ?? throw new InvalidDataException("En matchfil saknas i manifestet.");
                        await DownloadAsync(next, file, token);
                    }
                }
                next.CheckedAtUtc = due ? now() : current!.CheckedAtUtc;
                // Validate and parse a complete snapshot before publishing it to storage/UI.
                var data = Build(next, latestOnly, false, null);
                current = next;
                try { await store.WriteAsync(next, token); }
                catch (Exception ex) when (ex is not OperationCanceledException)
                { notice = "Resultaten är aktuella. Webbläsaren kunde inte spara dem för användning offline."; }
                return data with { Notice = notice };
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                if (current == null) throw new InvalidDataException("Resultaten kunde inte hämtas. Kontrollera anslutningen och försök igen.", ex);
                return Build(current, latestOnly, true,
                    "Kunde inte hämta aktuella resultat. Sparade resultat visas; de kan vara inaktuella.");
            }
        }
        finally { gate.Release(); }
    }

    async Task DownloadAsync(PublishedSnapshot snapshot, ChagiDataManifestFile file, CancellationToken token)
    {
        if (snapshot.Files.ContainsKey(file.Path)) return;
        var escaped = string.Join('/', file.Path.Split('/').Select(Uri.EscapeDataString));
        var bytes = await http.GetByteArrayAsync(new Uri(baseUri, escaped), token);
        if (!DataIntegrity.MatchesChecksum(bytes, file)) throw new InvalidDataException($"Felaktig checksumma: {file.Path}");
        snapshot.Files[file.Path] = bytes;
    }

    static List<ChagiDataManifestFile> SelectResults(IEnumerable<ChagiDataManifestFile> files, bool latestOnly)
    {
        var results = files.Where(f => f.Path.StartsWith("results/", StringComparison.Ordinal) && f.Path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(f => f.Path, StringComparer.Ordinal).ToList();
        return latestOnly ? results.Take(3).ToList() : results;
    }

    ResultsData Build(PublishedSnapshot snapshot, bool latestOnly, bool stale, string? notice)
    {
        var entries = DataIntegrity.ValidateManifest(snapshot.Manifest);
        var results = SelectResults(entries, latestOnly);
        byte[] Verified(ChagiDataManifestFile file) => snapshot.Files.TryGetValue(file.Path, out var bytes) && DataIntegrity.MatchesChecksum(bytes, file)
            ? bytes : throw new InvalidDataException("Sparade resultat är ofullständiga. Anslut till internet och försök igen.");
        var history = new List<CompetitionMatchHistory>();
        var index = entries.SingleOrDefault(f => f.Path == "matches/index.json");
        if (index != null)
        {
            var dates = results.Select(f => f.Path["results/".Length..].Split('_')[0]).ToHashSet();
            foreach (var entry in CompetitionMatchParser.ParseIndex(Text(Verified(index)))
                .Where(e => e.CompetitionDate.Date <= now().ToLocalTime().Date && (!latestOnly || dates.Contains(e.CompetitionDate.ToString("yyyy-MM-dd")))))
            {
                var file = entries.SingleOrDefault(f => f.Path == "matches/" + entry.File)
                    ?? throw new InvalidDataException("En matchfil saknas i manifestet.");
                history.Add(new(entry.CompetitionDate, entry.CompetitionName, CompetitionMatchParser.DeserializeMatches(Text(Verified(file)))));
            }
        }
        var fighters = CompetitorResultsParser.Parse(results.Select(f => (f.Path, Text(Verified(f)))), history);
        return new(fighters, CompetitionCatalogService.BuildCatalog(fighters), history, snapshot.Manifest.Version,
            snapshot.CheckedAtUtc, stale, latestOnly, notice);
    }

    static string Text(byte[] bytes) => Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF');
}
