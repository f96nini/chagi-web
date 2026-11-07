using System.Text;
using ITF_Taekwondo_Gradering.Logic;
using ITF_Taekwondo_Gradering.Models.Ranking;

namespace Chagi.Web.Services;

public sealed record SparringMatchSummaryRow(string Participant, string Name, string Club,
    decimal? Points, int Wins, decimal? Flags, int FinishedMatches);

public static class SparringMatchSummaryService
{
    public static IReadOnlyList<SparringMatchSummaryRow> Build(
        IEnumerable<DivisionMatch> matches, IEnumerable<CompetitionPlacementEntry>? placements = null)
    {
        var participants = new Dictionary<string, ParticipantTotals>(StringComparer.Ordinal);
        foreach (var match in matches.Where(match =>
            string.Equals(match.Discipline, "SPARRING", StringComparison.OrdinalIgnoreCase)))
        {
            var red = Add(match.RedParticipant, match.RedId.HasValue);
            var blue = Add(match.BlueParticipant, match.HasBlueParticipant);
            if (match.Finished != 1 || red is null || blue is null) continue;

            var redWon = match.WinnerId.HasValue
                ? match.WinnerId == match.RedId
                : IsWinner(match.Winner, match.RedParticipant, match.RedPoints, match.BluePoints);
            var blueWon = match.WinnerId.HasValue
                ? match.WinnerId == match.BlueId
                : IsWinner(match.Winner, match.BlueParticipant, match.BluePoints, match.RedPoints);
            red.Record(match.RedPoints, match.RedFlags, redWon);
            blue.Record(match.BluePoints, match.BlueFlags, blueWon);
        }

        if (placements is not null)
            foreach (var placement in placements)
                Add($"{placement.CompetitorName} - {placement.Team}", true);

        return participants.Values
            .Select(item => new SparringMatchSummaryRow(item.Participant, item.Name, item.Club,
                item.HasPoints ? item.Points : null, item.Wins,
                item.HasFlags ? item.Flags : null, item.FinishedMatches))
            .OrderByDescending(item => item.Points)
            .ThenByDescending(item => item.Wins)
            .ThenByDescending(item => item.Flags)
            .ThenBy(item => item.Name, SwedishNameComparer.Instance)
            .ThenBy(item => item.Club, SwedishNameComparer.Instance)
            .ToList();

        ParticipantTotals? Add(string? rawParticipant, bool known)
        {
            if (!known || string.IsNullOrWhiteSpace(rawParticipant)) return null;
            var participant = ClubCatalogService.NormalizeParticipant(rawParticipant);
            if (string.IsNullOrWhiteSpace(participant) || participant == "?") return null;
            var key = participant.Trim().Normalize(NormalizationForm.FormC).ToUpperInvariant();
            if (participants.TryGetValue(key, out var existing)) return existing;
            var parts = participant.Split(" - ", 2, StringSplitOptions.TrimEntries);
            var entry = new ParticipantTotals(participant, parts[0], parts.Length > 1 ? parts[1] : "");
            participants.Add(key, entry);
            return entry;
        }
    }

    static bool IsWinner(string? winner, string? participant, decimal? points, decimal? opponentPoints) =>
        !string.IsNullOrWhiteSpace(winner)
            ? string.Equals(winner, participant, StringComparison.OrdinalIgnoreCase)
            : points.HasValue && opponentPoints.HasValue && points > opponentPoints;

    sealed class ParticipantTotals(string participant, string name, string club)
    {
        public string Participant { get; } = participant;
        public string Name { get; } = name;
        public string Club { get; } = club;
        public decimal Points { get; private set; }
        public decimal Flags { get; private set; }
        public int Wins { get; private set; }
        public int FinishedMatches { get; private set; }
        public bool HasPoints { get; private set; }
        public bool HasFlags { get; private set; }

        public void Record(decimal? points, decimal? flags, bool won)
        {
            FinishedMatches++;
            if (points.HasValue) { Points += points.Value; HasPoints = true; }
            if (flags.HasValue) { Flags += flags.Value; HasFlags = true; }
            if (won) Wins++;
        }
    }
}
