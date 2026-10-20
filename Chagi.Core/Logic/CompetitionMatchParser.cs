using System.Text.Json;
using System.Text.Json.Serialization;
using ITF_Taekwondo_Gradering.Models.Ranking;
using ITF_Taekwondo_Gradering.WebConnection;

namespace ITF_Taekwondo_Gradering.Logic;

public static class CompetitionMatchParser
{
    static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    public static List<MatchDataIndexEntry> ParseIndex(string json) =>
        JsonSerializer.Deserialize<List<MatchDataIndexEntry>>(json, Options) ?? throw new JsonException("Missing match index.");

    public static MatchDataIndexEntry? FindEntry(IEnumerable<MatchDataIndexEntry> entries, DateTime date, string name) =>
        entries.SingleOrDefault(entry => entry.CompetitionDate.Date == date.Date &&
            string.Equals(entry.CompetitionName.Trim(), name.Trim(), StringComparison.OrdinalIgnoreCase));

    public static string ValidateFileName(string file)
    {
        if (string.IsNullOrWhiteSpace(file) || file.IndexOfAny(['/', '\\', ':']) >= 0 || file.Contains("..") || !file.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            throw new JsonException("Invalid match data filename.");
        return file;
    }

    public static IReadOnlyList<DivisionMatch> ParseMatches(string json, string division) =>
        DeserializeMatches(json)
        .Where(match => string.Equals(match.Category.Trim(), division.Trim(), StringComparison.OrdinalIgnoreCase))
        .DistinctBy(match => match.MatchId)
        .OrderBy(match => match.StartTime ?? DateTime.MaxValue)
        .ThenBy(match => match.MatchNumber)
        .ThenBy(match => match.MatchId)
        .ToList();

    public static List<DivisionMatch> DeserializeMatches(string json)
    {
        var matches = JsonSerializer.Deserialize<List<DivisionMatch>>(json, Options)
            ?? throw new JsonException("Missing match data.");
        foreach (var match in matches)
        {
            match.RedParticipant = ClubCatalogService.NormalizeParticipant(match.RedParticipant);
            match.BlueParticipant = ClubCatalogService.NormalizeParticipant(match.BlueParticipant);
            match.Winner = ClubCatalogService.NormalizeParticipant(match.Winner);
        }
        return matches;
    }
}
