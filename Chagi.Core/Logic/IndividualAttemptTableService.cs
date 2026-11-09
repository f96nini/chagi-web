using System.Globalization;
using System.Text;
using ITF_Taekwondo_Gradering.Models.Ranking;

namespace ITF_Taekwondo_Gradering.Logic;

public sealed record IndividualAttemptTableRow(int Position, string Name, decimal? Best, decimal? Worst, bool HasSecondResult);

public static class IndividualAttemptTableService
{
    public static IReadOnlyList<IndividualAttemptTableRow> Build(IEnumerable<DivisionMatch> matches, bool isSpec)
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
                return new IndividualAttemptTableRow(0, name, best, worst, !isSpec && scores.Count > 1);
            })
            .OrderByDescending(row => row.Best.HasValue)
            .ThenByDescending(row => row.Best)
            .ThenBy(row => row.Name, SwedishNameComparer.Instance)
            .ToList();

        var position = 0;
        for (var index = 0; index < rows.Count; index++)
        {
            if (!rows[index].Best.HasValue) continue;
            if (index == 0 || rows[index].Best != rows[index - 1].Best)
                position = index + 1;
            rows[index] = rows[index] with { Position = position };
        }
        return rows;
    }
}
