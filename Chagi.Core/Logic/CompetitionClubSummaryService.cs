using ITF_Taekwondo_Gradering.Models.Clubs;

namespace ITF_Taekwondo_Gradering.Logic;

public static class CompetitionClubSummaryService
{
    public static IReadOnlyList<CompetitionClubStandingEntry> BuildStandings(IEnumerable<CompetitionDivisionEntry> divisions)
    {
        var accumulators = new Dictionary<string, ClubStandingAccumulator>(StringComparer.OrdinalIgnoreCase);

        foreach (var division in divisions)
        {
            foreach (var result in division.Results)
            {
                var clubName = ResolveClubName(result.Team);
                var clubKey = ResolveClubKey(result.Team);

                if (!accumulators.TryGetValue(clubKey, out var accumulator))
                {
                    accumulator = new ClubStandingAccumulator(clubName);
                    accumulators[clubKey] = accumulator;
                }

                accumulator.TotalPoints += CalculatePoints(result.Placement);

                switch (result.Placement)
                {
                    case 1:
                        accumulator.GoldCount++;
                        break;
                    case 2:
                        accumulator.SilverCount++;
                        break;
                    case 3:
                        accumulator.BronzeCount++;
                        break;
                }
            }
        }

        var orderedEntries = RankingPositionService
            .OrderByScoreAndMedals(
                accumulators.Values,
                entry => entry.TotalPoints,
                entry => entry.GoldCount,
                entry => entry.SilverCount,
                entry => entry.BronzeCount)
            .ThenBy(entry => entry.ClubName, SwedishNameComparer.Instance)
            .ToList();

        var ranks = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        RankingPositionService.ApplySharedPositions(
            orderedEntries,
            (left, right) => RankingPositionService.HaveSameScoreAndMedals(
                left,
                right,
                entry => entry.TotalPoints,
                entry => entry.GoldCount,
                entry => entry.SilverCount,
                entry => entry.BronzeCount),
            (entry, rank) => ranks[entry.ClubName] = rank);

        return orderedEntries
            .Select(entry => new CompetitionClubStandingEntry
            {
                Rank = ranks[entry.ClubName],
                ClubName = entry.ClubName,
                GoldCount = entry.GoldCount,
                SilverCount = entry.SilverCount,
                BronzeCount = entry.BronzeCount,
                TotalPoints = entry.TotalPoints
            })
            .ToList();
    }

    static string ResolveClubKey(string? team)
    {
        if (string.IsNullOrWhiteSpace(team))
            return "unknown-club";

        var club = ClubCatalogService.ResolveClub(team);
        if (club != null)
            return club.Id;

        return team.Trim().ToUpperInvariant();
    }

    static string ResolveClubName(string? team)
    {
        if (string.IsNullOrWhiteSpace(team))
            return "Okänd klubb";

        return ClubCatalogService.ResolveClub(team)?.Name ?? team.Trim();
    }

    static int CalculatePoints(int placement)
    {
        return placement switch
        {
            1 => 5,
            2 => 3,
            3 => 1,
            _ => 0
        };
    }

    sealed class ClubStandingAccumulator(string clubName)
    {
        public string ClubName { get; } = clubName;
        public int GoldCount { get; set; }
        public int SilverCount { get; set; }
        public int BronzeCount { get; set; }
        public int TotalPoints { get; set; }
    }
}
