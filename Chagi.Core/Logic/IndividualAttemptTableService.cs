using System.Globalization;
using System.Text;
using ITF_Taekwondo_Gradering.Models.Ranking;

namespace ITF_Taekwondo_Gradering.Logic;

public sealed record IndividualAttemptTableRow(int Position, string Name, decimal? Best, decimal? Worst, bool HasSecondResult);

public static class IndividualAttemptTableService
{
    public static IReadOnlyList<IndividualAttemptTableRow> Build(
        IEnumerable<DivisionMatch> matches, bool isSpec, IEnumerable<CompetitionPlacementEntry>? placements = null)
    {
        var rows = matches
            .Where(match => isSpec ? match.Discipline == "SPEC_TECH" : match.Discipline is "POWER" or "POWER_BREAK")
            .Where(match => !string.IsNullOrWhiteSpace(match.RedParticipant))
            .GroupBy(match => match.RedId?.ToString(CultureInfo.InvariantCulture) ??
                match.RedParticipant!.Trim().Normalize(NormalizationForm.FormC).ToUpperInvariant(), StringComparer.Ordinal)
            .Select(group =>
            {
                var finished = group.Where(match => match.Finished == 1).ToList();
                var scores = finished.SelectMany(match => match.IndividualScores)
                    .Where(score => score.Eligible && score.Points.HasValue)
                    .Select(score => score.Points!.Value).ToList();
                var heights = finished.Select(match => match.HighestClearedHeightCm ??
                    match.IndividualScores.Where(score => score.Eligible && score.Cleared == true)
                        .Max(score => score.HeightCm)).ToList();
                var best = isSpec ? heights.Max() : scores.Count > 0 ? scores.Max() : (decimal?)null;
                var worst = !isSpec && scores.Count > 1 ? scores.Min() : (decimal?)null;
                var name = group.First().RedParticipant!.Split(" - ", 2)[0];
                return (Row: new IndividualAttemptTableRow(0, name, best, worst, !isSpec && scores.Count > 1),
                    Participant: Identity(group.First().RedParticipant));
            })
            .OrderByDescending(item => item.Row.Best)
            .ThenByDescending(item => item.Row.Worst)
            .ToList();

        if (placements is not null)
        {
            var publishedPositions = placements
                .GroupBy(placement => Identity($"{placement.CompetitorName} - {placement.Team}"), StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First().Placement, StringComparer.Ordinal);
            return rows
                .Select(item => item.Row with { Position = publishedPositions.GetValueOrDefault(item.Participant) })
                .OrderBy(row => row.Position > 0 ? row.Position : int.MaxValue)
                .ThenByDescending(row => row.Best)
                .ThenByDescending(row => row.Worst)
                .ToList();
        }

        var rankedRows = rows.Select(item => item.Row).ToList();
        var position = 0;
        for (var index = 0; index < rankedRows.Count; index++)
        {
            if (!rankedRows[index].Best.HasValue) continue;
            if (index == 0 || rankedRows[index].Best != rankedRows[index - 1].Best)
                position = index + 1;
            rankedRows[index] = rankedRows[index] with { Position = position };
        }
        return rankedRows;
    }

    static string Identity(string? participant) =>
        (ClubCatalogService.NormalizeParticipant(participant) ?? string.Empty)
            .Trim().Normalize(NormalizationForm.FormC).ToUpperInvariant();
}
