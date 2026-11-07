using ITF_Taekwondo_Gradering.Logic;
using ITF_Taekwondo_Gradering.Models.Ranking;

namespace Chagi.Web.Services;

public enum FighterHistorySortColumn { Competition, Division, Placement, Points }

public sealed record FighterHistoryGroup(
    DateTime Date,
    string CompetitionName,
    string CompetitionCity,
    IReadOnlyList<CompetitionResult> Results)
{
    public string FirstDivision => Results.Select(ResultDivision).OrderBy(value => value, SwedishNameComparer.Instance).First();
    public int BestPlacement => Results.Where(result => result.Placement > 0).Select(result => result.Placement).DefaultIfEmpty(int.MaxValue).Min();
    public int TotalPoints => Results.Sum(result => result.PointsAwarded);

    internal static string ResultDivision(CompetitionResult result) => $"{result.Discipline} / {result.Division}";
}

public static class FighterHistorySortService
{
    public static List<FighterHistoryGroup> Sort(
        IEnumerable<CompetitionResult> results,
        FighterHistorySortColumn column,
        bool ascending)
    {
        var groups = results
            .GroupBy(result => (result.CompetitionDate.Date, result.CompetitionName, result.CompetitionCity))
            .Select(group => new FighterHistoryGroup(group.Key.Date, group.Key.CompetitionName, group.Key.CompetitionCity, group.ToList()))
            .ToList();

        IOrderedEnumerable<FighterHistoryGroup> ordered = column switch
        {
            FighterHistorySortColumn.Competition when ascending => groups.OrderBy(group => group.Date),
            FighterHistorySortColumn.Competition => groups.OrderByDescending(group => group.Date),
            FighterHistorySortColumn.Division when ascending => groups.OrderBy(group => group.FirstDivision, SwedishNameComparer.Instance),
            FighterHistorySortColumn.Division => groups.OrderByDescending(group => group.FirstDivision, SwedishNameComparer.Instance),
            FighterHistorySortColumn.Placement when ascending => groups.OrderBy(group => group.BestPlacement),
            FighterHistorySortColumn.Placement => groups.OrderBy(group => group.BestPlacement == int.MaxValue).ThenByDescending(group => group.BestPlacement),
            FighterHistorySortColumn.Points when ascending => groups.OrderBy(group => group.TotalPoints),
            _ => groups.OrderByDescending(group => group.TotalPoints)
        };

        return ordered
            .ThenBy(group => group.CompetitionName, SwedishNameComparer.Instance)
            .ThenBy(group => group.CompetitionCity, SwedishNameComparer.Instance)
            .ThenByDescending(group => group.Date)
            .Select(group => group with { Results = SortRows(group.Results, column, ascending) })
            .ToList();
    }

    static List<CompetitionResult> SortRows(
        IReadOnlyList<CompetitionResult> results,
        FighterHistorySortColumn column,
        bool ascending)
    {
        IOrderedEnumerable<CompetitionResult> ordered = column switch
        {
            FighterHistorySortColumn.Division when ascending => results.OrderBy(FighterHistoryGroup.ResultDivision, SwedishNameComparer.Instance),
            FighterHistorySortColumn.Division => results.OrderByDescending(FighterHistoryGroup.ResultDivision, SwedishNameComparer.Instance),
            FighterHistorySortColumn.Placement when ascending => results.OrderBy(result => result.Placement <= 0).ThenBy(result => result.Placement),
            FighterHistorySortColumn.Placement => results.OrderBy(result => result.Placement <= 0).ThenByDescending(result => result.Placement),
            FighterHistorySortColumn.Points when ascending => results.OrderBy(result => result.PointsAwarded),
            FighterHistorySortColumn.Points => results.OrderByDescending(result => result.PointsAwarded),
            _ => results.OrderBy(result => result.Discipline)
        };
        return ordered.ThenBy(result => result.Division, SwedishNameComparer.Instance).ToList();
    }
}
