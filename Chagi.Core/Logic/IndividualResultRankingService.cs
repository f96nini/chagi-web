using System.Text;
using ITF_Taekwondo_Gradering.Models.Ranking;

namespace ITF_Taekwondo_Gradering.Logic;

public sealed record IndividualResultRow(CompetitionPlacementEntry Participant, decimal? Result, int Placement);

public sealed record IndividualResultTable(bool ShowResultColumn, List<IndividualResultRow> Rows);

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
            return new IndividualResultRow(participant, found ? result : null, participant.Placement);
        }).ToList();
        var hasResult = rows.Any(row => row.Result.HasValue);
        if (!hasResult) return OriginalRows(division);
        return new(true, rows);
    }

    static IndividualResultTable OriginalRows(CompetitionDivisionEntry division) =>
        new(false, division.Results.Select(result => new IndividualResultRow(result, null, result.Placement)).ToList());

    static string Identity(string participant) => participant.Trim().Normalize(NormalizationForm.FormC).ToUpperInvariant();
}
