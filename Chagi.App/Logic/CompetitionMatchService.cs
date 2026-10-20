using System.Text.Json;
using static ITF_Taekwondo_Gradering.Logic.CompetitionMatchParser;
using System.Text.Json.Serialization;
using ITF_Taekwondo_Gradering.Models.Ranking;
using ITF_Taekwondo_Gradering.WebConnection;

namespace ITF_Taekwondo_Gradering.Logic;

public static class CompetitionMatchService
{
    static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    public static Task<IReadOnlyList<CompetitionMatchHistory>> GetHistoryAsync(string? snapshotDirectory = null)
        => Task.Run(() => GetHistoryCoreAsync(snapshotDirectory));

    static async Task<IReadOnlyList<CompetitionMatchHistory>> GetHistoryCoreAsync(string? snapshotDirectory)
    {
        snapshotDirectory ??= SyncedDataStore.GetDirectory(FileSystem.AppDataDirectory);
        await using var stream = await FileSystem.OpenAppPackageFileAsync("matches/index.json").ConfigureAwait(false);
        using var reader = new StreamReader(stream);
        var entries = ParseIndex(await reader.ReadToEndAsync().ConfigureAwait(false));
        var syncedIndex = Path.Combine(snapshotDirectory, "matches", "index.json");
        if (File.Exists(syncedIndex))
            entries.AddRange(ParseIndex(await File.ReadAllTextAsync(syncedIndex).ConfigureAwait(false)));

        var history = new List<CompetitionMatchHistory>();
        foreach (var entry in entries
                     .Where(entry => entry.CompetitionDate.Date <= DateTime.Today)
                     .DistinctBy(entry => $"{entry.CompetitionDate:yyyy-MM-dd}|{entry.CompetitionName.Trim()}", StringComparer.OrdinalIgnoreCase))
        {
            // Use the same source precedence as the competition details view.
            var json = await LoadMatchesJsonAsync(entry.CompetitionDate, entry.CompetitionName, snapshotDirectory: snapshotDirectory).ConfigureAwait(false);
            if (json != null)
                history.Add(new(entry.CompetitionDate, entry.CompetitionName, DeserializeMatches(json)));
        }
        return history;
    }

    public static Task<IReadOnlyList<DivisionMatch>> GetMatchesAsync(DateTime date, string competitionName, string division,
        CancellationToken cancellationToken = default)
        => Task.Run(() => GetMatchesCoreAsync(date, competitionName, division, cancellationToken), cancellationToken);

    static async Task<IReadOnlyList<DivisionMatch>> GetMatchesCoreAsync(DateTime date, string competitionName, string division,
        CancellationToken cancellationToken)
    {
        var json = await LoadMatchesJsonAsync(date, competitionName, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return json == null ? Array.Empty<DivisionMatch>() : ParseMatches(json, division);
    }

    public static Task<HashSet<string>> GetAvailableDivisionsAsync(DateTime date, string competitionName,
        CancellationToken cancellationToken = default)
        => Task.Run(() => GetAvailableDivisionsCoreAsync(date, competitionName, cancellationToken), cancellationToken);

    static async Task<HashSet<string>> GetAvailableDivisionsCoreAsync(DateTime date, string competitionName,
        CancellationToken cancellationToken)
    {
        var json = await LoadMatchesJsonAsync(date, competitionName, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        var matches = json == null ? [] : DeserializeMatches(json);
        return matches.Where(match => match.HasDetailedData)
            .Select(match => match.Category.Trim())
            .Where(category => category.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    static async Task<string?> LoadMatchesJsonAsync(DateTime date, string competitionName,
        CancellationToken cancellationToken = default, string? snapshotDirectory = null)
    {
        var syncedDirectory = Path.Combine(snapshotDirectory ?? SyncedDataStore.GetDirectory(FileSystem.AppDataDirectory), "matches");
        var indexPath = Path.Combine(syncedDirectory, "index.json");
        if (File.Exists(indexPath))
        {
            var index = ParseIndex(await File.ReadAllTextAsync(indexPath, cancellationToken).ConfigureAwait(false));
            var entry = FindEntry(index, date, competitionName);
            if (entry != null)
            {
                return await File.ReadAllTextAsync(Path.Combine(syncedDirectory, ValidateFileName(entry.File)), cancellationToken).ConfigureAwait(false);
            }
        }

        // Packaged data makes the included competitions available offline and in debug builds.
        await using var indexStream = await FileSystem.OpenAppPackageFileAsync("matches/index.json").ConfigureAwait(false);
        using var indexReader = new StreamReader(indexStream);
        var packagedEntry = FindEntry(ParseIndex(await indexReader.ReadToEndAsync(cancellationToken).ConfigureAwait(false)), date, competitionName);
        if (packagedEntry == null)
            return null;
        await using var matchStream = await FileSystem.OpenAppPackageFileAsync($"matches/{ValidateFileName(packagedEntry.File)}").ConfigureAwait(false);
        using var matchReader = new StreamReader(matchStream);
        return await matchReader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
    }

    public static IReadOnlyList<DivisionMatch> ParseMatches(string json, string division) => CompetitionMatchParser.ParseMatches(json, division);
}
