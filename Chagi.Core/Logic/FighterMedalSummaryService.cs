using ITF_Taekwondo_Gradering.Models.Ranking;
using ITF_Taekwondo_Gradering.Views;

namespace ITF_Taekwondo_Gradering.Logic;

public static class FighterMedalSummaryService
{
    public static MedalSummaryRowModel Build(Competitor fighter, IEnumerable<Competitor> competitors,
        CompetitionDisciplineE discipline)
    {
        var results = ResultsFor(fighter, discipline).ToList();
        var own = Score(results);
        var medalPosition = 0;
        var pointsPosition = 0;
        // As in the existing league, compare fighters of the same gender across all classes.
        if (fighter.IsMale.HasValue && results.Count > 0)
        {
            var peers = competitors.Where(c => c.IsMale == fighter.IsMale)
                .Select(c => Score(ResultsFor(c, discipline))).ToList();
            medalPosition = 1 + peers.Count(peer => peer.Medals > own.Medals);
            // Preserve the points league's 5/3/1 score and gold/silver/bronze tie breakers.
            pointsPosition = 1 + peers.Count(peer => peer.Points > own.Points
                || peer.Points == own.Points && (peer.Gold > own.Gold
                    || peer.Gold == own.Gold && (peer.Silver > own.Silver
                        || peer.Silver == own.Silver && peer.Bronze > own.Bronze)));
        }

        return new MedalSummaryRowModel
        {
            GoldCount = own.Gold, SilverCount = own.Silver, BronzeCount = own.Bronze,
            CompetitionCount = results.Select(r => $"{r.CompetitionDate:yyyy-MM-dd}|{r.CompetitionName.Trim()}|{r.CompetitionCity.Trim()}")
                .Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            LeaguePosition = medalPosition, PointsLeaguePosition = pointsPosition
        };
    }

    static IEnumerable<CompetitionResult> ResultsFor(Competitor fighter, CompetitionDisciplineE discipline) =>
        fighter.Results.Where(r => discipline == CompetitionDisciplineE.Alla || r.Discipline == discipline);

    static MedalScore Score(IEnumerable<CompetitionResult> results)
    {
        var medals = results.Where(r => r.Placement is >= 1 and <= 3).ToList();
        return new(medals.Count(r => r.Placement == 1), medals.Count(r => r.Placement == 2), medals.Count(r => r.Placement == 3));
    }

    sealed record MedalScore(int Gold, int Silver, int Bronze)
    {
        public int Medals => Gold + Silver + Bronze;
        public int Points => Gold * 5 + Silver * 3 + Bronze;
    }
}
