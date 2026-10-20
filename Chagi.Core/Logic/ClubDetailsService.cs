using System.Globalization;
using ITF_Taekwondo_Gradering.Models.Clubs;
using ITF_Taekwondo_Gradering.Models.Ranking;

namespace ITF_Taekwondo_Gradering.Logic;

public static class ClubDetailsService
{
    static readonly StringComparer Names = StringComparer.Create(CultureInfo.GetCultureInfo("sv-SE"), true);

    public static ClubDetailsSnapshot Build(IEnumerable<Competitor> competitors, string clubId,
        GenderFilterMode gender, CompetitionDisciplineE discipline, IReadOnlyCollection<RankingAgeClassE> enabledClasses)
    {
        // Match the Fighters list: class filters refer to the fighter's current age class.
        var participants = competitors
            .Where(fighter => ClubCatalogService.ResolveClub(fighter.Team)?.Id == clubId)
            .Where(fighter => gender switch
            {
                GenderFilterMode.Male => fighter.IsMale == true,
                GenderFilterMode.Female => fighter.IsMale == false,
                _ => true
            })
            .Where(fighter => enabledClasses.Count > 0 &&
                (!fighter.CurrentAgeClass.HasValue || enabledClasses.Contains(fighter.CurrentAgeClass.Value)))
            .Select(fighter => (Fighter: fighter, Results: fighter.Results
                .Where(result => discipline == CompetitionDisciplineE.Alla || result.Discipline == discipline).ToList()))
            .ToList();

        var fighters = participants.Select(participant => new RankedCompetitorEntry
        {
            Competitor = participant.Fighter,
            FullName = participant.Fighter.FullName,
            Team = participant.Fighter.Team,
            Score = participant.Results.Sum(result => MedalPoints(result.Placement)),
            ResultCount = participant.Results.Count,
            GoldCount = participant.Results.Count(result => result.Placement == 1),
            SilverCount = participant.Results.Count(result => result.Placement == 2),
            BronzeCount = participant.Results.Count(result => result.Placement == 3),
            HasResults = participant.Results.Count > 0,
            IsFavorite = participant.Fighter.IsFavorite
        }).ToList();
        fighters = RankingPositionService.OrderByScoreAndMedals(fighters,
                fighter => fighter.Score, fighter => fighter.GoldCount, fighter => fighter.SilverCount, fighter => fighter.BronzeCount)
            .ThenBy(fighter => fighter.FullName, Names).ToList();
        RankingPositionService.ApplySharedPositions(fighters,
            (left, right) => RankingPositionService.HaveSameScoreAndMedals(left, right,
                fighter => fighter.Score, fighter => fighter.GoldCount, fighter => fighter.SilverCount, fighter => fighter.BronzeCount),
            (fighter, position) => fighter.Position = position,
            (fighter, label) => fighter.PositionLabel = label);

        var competitions = participants.SelectMany(participant => participant.Results
                .Select(result => (FighterId: participant.Fighter.Id, Result: result)))
            .GroupBy(entry => (CompetitionDate: entry.Result.CompetitionDate.Date, entry.Result.CompetitionName, entry.Result.CompetitionCity))
            .Select(group => new ClubCompetitionEntry
            {
                Date = group.Key.CompetitionDate,
                Name = group.Key.CompetitionName,
                City = group.Key.CompetitionCity,
                TotalPoints = group.Sum(entry => MedalPoints(entry.Result.Placement)),
                GoldCount = group.Count(entry => entry.Result.Placement == 1),
                SilverCount = group.Count(entry => entry.Result.Placement == 2),
                BronzeCount = group.Count(entry => entry.Result.Placement == 3),
                FighterCount = group.Select(entry => entry.FighterId).Distinct().Count()
            });
        return new ClubDetailsSnapshot { Fighters = fighters, Competitions = SortCompetitions(competitions) };
    }

    public static IReadOnlyList<ClubCompetitionEntry> SortCompetitions(IEnumerable<ClubCompetitionEntry> competitions,
        ClubCompetitionSortMode sortMode = ClubCompetitionSortMode.Date)
    {
        var ordered = sortMode == ClubCompetitionSortMode.MedalPoints
            ? RankingPositionService.OrderByScoreAndMedals(competitions,
                competition => competition.TotalPoints, competition => competition.GoldCount,
                competition => competition.SilverCount, competition => competition.BronzeCount)
                .ThenByDescending(competition => competition.Date)
            : competitions.OrderByDescending(competition => competition.Date);
        return ordered.ThenBy(competition => competition.Name, Names)
            .ThenBy(competition => competition.City, Names).ToList();
    }

    static int MedalPoints(int placement) => placement switch { 1 => 5, 2 => 3, 3 => 1, _ => 0 };
}
