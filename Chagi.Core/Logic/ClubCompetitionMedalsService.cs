using ITF_Taekwondo_Gradering.Models.Ranking;

namespace ITF_Taekwondo_Gradering.Logic;

public static class ClubCompetitionMedalsService
{
    public static IReadOnlyList<CompetitionDivisionEntry> BuildDivisions(CompetitionCatalogEntry competition,
        string clubId, GenderFilterMode gender, CompetitionDisciplineE discipline,
        IReadOnlyCollection<RankingAgeClassE> enabledClasses)
    {
        // Use the competition's gender and age classes, just like its full results page.
        return competition.Divisions
            .Where(division => gender == GenderFilterMode.All ||
                (gender == GenderFilterMode.Male ? division.IsMale != false : division.IsMale != true))
            .Where(division => enabledClasses.Count > 0 &&
                (!division.AgeClass.HasValue || enabledClasses.Contains(division.AgeClass.Value)))
            .Where(division => discipline == CompetitionDisciplineE.Alla || division.Discipline == discipline)
            .Select(division => new CompetitionDivisionEntry
            {
                Discipline = division.Discipline, BeltGroup = division.BeltGroup, CategoryClass = division.CategoryClass,
                Division = division.Division, IsMale = division.IsMale, AgeClass = division.AgeClass,
                Results = division.Results.Where(result => result.Placement is >= 1 and <= 3 &&
                    ClubCatalogService.ResolveClub(result.Team)?.Id == clubId).ToList()
            })
            .Where(division => division.Results.Count > 0)
            .ToList();
    }
}
