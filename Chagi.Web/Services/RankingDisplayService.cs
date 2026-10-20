using ITF_Taekwondo_Gradering.Logic;
using ITF_Taekwondo_Gradering.Models.Ranking;

namespace Chagi.Web.Services;

public enum RankingSortMode { TotalPoints, Class, Club }

public sealed record RankingDisplayRow(RankedCompetitorEntry Entry, string ClassName);

public sealed record RankingDisplayGroup(string Title, string Label, List<RankingDisplayRow> Rows);

public static class RankingDisplayService
{
    public static List<RankingDisplayGroup> BuildGroups(
        IEnumerable<Competitor> competitors,
        bool? showMale,
        CompetitionDisciplineE discipline,
        RankingAgeClassE? ageClass,
        RankingSortMode sortMode)
    {
        var sections = new List<(string Gender, RankingSection Section)>();
        foreach (var male in new[] { true, false }.Where(value => !showMale.HasValue || showMale.Value == value))
        {
            foreach (var section in RankingService.BuildAllSections(competitors, male, discipline, CompetitorSortE.TotalPoints)
                         .Where(section => !ageClass.HasValue || section.AgeClass == ageClass.Value))
            {
                sections.Add((male ? "Herrar / pojkar" : "Damer / flickor", section));
            }
        }

        if (sortMode == RankingSortMode.Class)
        {
            return sections.Select(item => new RankingDisplayGroup(
                item.Section.Title,
                item.Gender,
                item.Section.Entries.Select(entry => new RankingDisplayRow(entry, item.Section.Title)).ToList()))
                .ToList();
        }

        var rankedRows = RankingPositionService.OrderByScoreAndMedals(
                sections.SelectMany(item => item.Section.Entries.Select(entry => new RankingDisplayRow(entry, item.Section.Title))),
                row => row.Entry.Score,
                row => row.Entry.GoldCount,
                row => row.Entry.SilverCount,
                row => row.Entry.BronzeCount)
            .ThenBy(row => row.Entry.FullName, SwedishNameComparer.Instance)
            .ThenBy(row => row.Entry.Team, SwedishNameComparer.Instance)
            .ToList();

        RankingPositionService.ApplySharedPositions(
            rankedRows,
            (left, right) => RankingPositionService.HaveSameScoreAndMedals(
                left, right,
                row => row.Entry.Score,
                row => row.Entry.GoldCount,
                row => row.Entry.SilverCount,
                row => row.Entry.BronzeCount),
            (row, position) => row.Entry.Position = position,
            (row, label) => row.Entry.PositionLabel = label);

        if (sortMode == RankingSortMode.Club)
        {
            return rankedRows
                .GroupBy(row => string.IsNullOrWhiteSpace(row.Entry.Team) ? "Ingen klubb" : row.Entry.Team.Trim(), StringComparer.OrdinalIgnoreCase)
                .OrderBy(group => group.Key, SwedishNameComparer.Instance)
                .Select(group => new RankingDisplayGroup(group.Key, $"{group.Count()} fighters", group.ToList()))
                .ToList();
        }

        return rankedRows.Count == 0
            ? []
            : [new RankingDisplayGroup("Total Poäng", $"{rankedRows.Count} fighters", rankedRows)];
    }
}
