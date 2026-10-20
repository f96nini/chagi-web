using System.Globalization;
using ITF_Taekwondo_Gradering.Models.Clubs;
using ITF_Taekwondo_Gradering.Models.Geography;
using ITF_Taekwondo_Gradering.Models.Ranking;

namespace ITF_Taekwondo_Gradering.Logic;

public static class ClubStatisticsService
{
    public static IReadOnlyList<ClubSummaryEntry> BuildClubSummaries(IEnumerable<Competitor> competitors)
    {
        var accumulators = ClubCatalogService.GetAllClubs()
            .ToDictionary(
                club => club.Id,
                club => new ClubAccumulator(club),
                StringComparer.OrdinalIgnoreCase);

        foreach (var competitor in competitors)
        {
            var club = ClubCatalogService.ResolveClub(competitor.Team);
            if (club == null) continue;
            if (!accumulators.TryGetValue(club.Id, out var accumulator))
                accumulators[club.Id] = accumulator = new ClubAccumulator(club);
            accumulator.FighterKeys.Add(competitor.FullName.Trim().Normalize());

            foreach (var result in competitor.Results)
            {
                accumulator.CompetitionKeys.Add(BuildCompetitionKey(result));

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

        var summaries = accumulators.Values
            .Select(accumulator => new ClubSummaryEntry
            {
                Id = accumulator.Club.Id,
                Name = accumulator.Club.Name,
                ImageSource = string.IsNullOrWhiteSpace(accumulator.Club.ImageSource)
                    ? ClubCatalogService.PlaceholderImage
                    : accumulator.Club.ImageSource!,
                CompetitionCount = accumulator.CompetitionKeys.Count,
                FighterCount = accumulator.FighterKeys.Count,
                GoldCount = accumulator.GoldCount,
                SilverCount = accumulator.SilverCount,
                BronzeCount = accumulator.BronzeCount
            })
            .ToList();

        var rankedSummaries = RankingPositionService
            .OrderByScoreAndMedals(
                summaries,
                summary => summary.TotalPoints,
                summary => summary.GoldCount,
                summary => summary.SilverCount,
                summary => summary.BronzeCount)
            .ThenBy(summary => summary.Name, SwedishNameComparer.Instance)
            .ToList();

        var ranks = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        RankingPositionService.ApplySharedPositions(
            rankedSummaries,
            (left, right) => RankingPositionService.HaveSameScoreAndMedals(
                left,
                right,
                summary => summary.TotalPoints,
                summary => summary.GoldCount,
                summary => summary.SilverCount,
                summary => summary.BronzeCount),
            (summary, rank) => ranks[summary.Id] = rank);

        return rankedSummaries
            .Select(summary => new ClubSummaryEntry
            {
                Rank = ranks[summary.Id],
                Id = summary.Id,
                Name = summary.Name,
                ImageSource = summary.ImageSource,
                CompetitionCount = summary.CompetitionCount,
                FighterCount = summary.FighterCount,
                GoldCount = summary.GoldCount,
                SilverCount = summary.SilverCount,
                BronzeCount = summary.BronzeCount
            })
            .ToList();
    }

    static string BuildCompetitionKey(CompetitionResult result)
    {
        var datePart = result.CompetitionDate == DateTime.MinValue
            ? string.Empty
            : result.CompetitionDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        return $"{datePart}|{result.CompetitionName}|{result.CompetitionCity}";
    }

    sealed class ClubAccumulator(ClubCatalogClub club)
    {
        public ClubCatalogClub Club { get; } = club;
        public HashSet<string> CompetitionKeys { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> FighterKeys { get; } = new(StringComparer.OrdinalIgnoreCase);
        public int GoldCount { get; set; }
        public int SilverCount { get; set; }
        public int BronzeCount { get; set; }
    }
}
