using System.Text;
using ITF_Taekwondo_Gradering.Logic;
using ITF_Taekwondo_Gradering.Models.Ranking;

namespace Chagi.Web.Services;

public sealed record IndividualResultRow(CompetitionPlacementEntry Participant, decimal? Result, int Placement);

public sealed record IndividualResultTable(bool ShowResultColumn, bool HasCalculatedPlacements, List<IndividualResultRow> Rows);

public static class IndividualResultRankingService
{
    public static IndividualResultTable Build(CompetitionDivisionEntry division, IEnumerable<DivisionMatch> matches)
    {
        if (division.Discipline is not (CompetitionDisciplineE.Specialtekniker or CompetitionDisciplineE.Kross))
            return OriginalRows(division);

        var isSpec = division.Discipline == CompetitionDisciplineE.Specialtekniker;
        var values = matches
            .Where(match => match.Finished == 1 && match.HasIndividualResults &&
                (isSpec ? match.Discipline == "SPEC_TECH" : match.Discipline is "POWER" or "POWER_BREAK"))
            .GroupBy(match => Identity(match.RedParticipant!), StringComparer.Ordinal)
            .ToDictionary(group => group.Key,
                group => isSpec ? group.Max(match => match.HighestClearedHeightCm) : group.Max(match => match.RedPoints),
                StringComparer.Ordinal);

        var rows = division.Results.Select(participant =>
        {
            var found = values.TryGetValue(Identity($"{participant.CompetitorName} - {participant.Team}"), out var result);
            return (Row: new IndividualResultRow(participant, found ? result : null, participant.Placement), Found: found);
        }).ToList();
        var hasResult = rows.Any(item => item.Row.Result.HasValue);
        if (!hasResult) return OriginalRows(division);
        if (rows.Any(item => !item.Found))
            return new(true, false, rows.Select(item => item.Row).ToList());

        var ranked = rows.Select(item => item.Row)
            .OrderByDescending(row => row.Result.HasValue)
            .ThenByDescending(row => row.Result)
            .ThenBy(row => row.Participant.CompetitorName, SwedishNameComparer.Instance)
            .ThenBy(row => row.Participant.Team, SwedishNameComparer.Instance)
            .ToList();
        var placement = 0;
        for (var index = 0; index < ranked.Count; index++)
        {
            if (index == 0 || ranked[index].Result != ranked[index - 1].Result)
                placement = index + 1;
            ranked[index] = ranked[index] with { Placement = placement };
        }
        return new(true, true, ranked);
    }

    static IndividualResultTable OriginalRows(CompetitionDivisionEntry division) =>
        new(false, false, division.Results.Select(result => new IndividualResultRow(result, null, result.Placement)).ToList());

    static string Identity(string participant) => participant.Trim().Normalize(NormalizationForm.FormC).ToUpperInvariant();
}
