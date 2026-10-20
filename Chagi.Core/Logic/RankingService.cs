using ITF_Taekwondo_Gradering.Models;
using ITF_Taekwondo_Gradering.Models.Ranking;

namespace ITF_Taekwondo_Gradering.Logic
{
    public static class RankingService
    {
        public static List<RankingSection> BuildSections(
            IEnumerable<Competitor> competitors,
            bool showMale,
            CompetitionDisciplineE discipline)
        {
            return BuildSectionsInternal(competitors, showMale, discipline, topOnly: true, CompetitorSortE.TotalPoints);
        }

        public static List<RankingSection> BuildAllSections(
            IEnumerable<Competitor> competitors,
            bool showMale,
            CompetitionDisciplineE discipline,
            CompetitorSortE sortBy)
        {
            return BuildSectionsInternal(competitors, showMale, discipline, topOnly: false, sortBy);
        }

        static List<RankingSection> BuildSectionsInternal(
            IEnumerable<Competitor> competitors,
            bool showMale,
            CompetitionDisciplineE discipline,
            bool topOnly,
            CompetitorSortE sortBy)
        {
            var sections = new List<RankingSection>();

            foreach (var ageClass in Enum.GetValues<RankingAgeClassE>())
            {
                if (!RankingClassFilterService.IsEnabled(ageClass))
                    continue;

                foreach (var beltGroup in Enum.GetValues<RankingBeltGroupE>())
                {
                    var candidateEntries = competitors
                        .Where(c => c.IsMale.HasValue && c.IsMale.Value == showMale)
                        .Select(c => BuildEntry(c, discipline, ageClass, beltGroup))
                        .Where(e => e != null)
                        .Select(e => e!)
                        .ToList();

                    var rankedEntries = RankEntries(candidateEntries.Where(e => e.HasResults))
                        .ToList();

                    RankingPositionService.ApplySharedPositions(
                        rankedEntries,
                        HaveSameRank,
                        (entry, position) => entry.Position = position,
                        (entry, label) => entry.PositionLabel = label);

                    var noResultFavorites = candidateEntries
                        .Where(e => !e.HasResults && e.IsFavorite)
                        .OrderBy(e => e.FullName, SwedishNameComparer.Instance)
                        .ThenBy(e => e.Team, SwedishNameComparer.Instance)
                        .ToList();

                    foreach (var entry in noResultFavorites)
                    {
                        entry.Position = 0;
                        entry.PositionLabel = "-";
                    }

                    var entries = topOnly
                        ? rankedEntries
                            .Take(5)
                            .Concat(rankedEntries.Skip(5).Where(e => e.IsFavorite))
                            .Concat(noResultFavorites)
                            .ToList()
                        : OrderEntriesForDisplay(rankedEntries, sortBy)
                            .Concat(noResultFavorites)
                            .ToList();

                    if (entries.Count == 0)
                        continue;

                    sections.Add(new RankingSection
                    {
                        AgeClass = ageClass,
                        BeltGroup = beltGroup,
                        Title = $"{ToDisplayName(ageClass)} - {ToDisplayName(beltGroup)}",
                        Entries = entries
                    });
                }
            }

            return sections;
        }

        static IEnumerable<RankedCompetitorEntry> RankEntries(
            IEnumerable<RankedCompetitorEntry> entries)
        {
            return RankingPositionService
                .OrderByScoreAndMedals(
                    entries,
                    e => e.Score,
                    e => e.GoldCount,
                    e => e.SilverCount,
                    e => e.BronzeCount)
                .ThenBy(e => e.FullName, SwedishNameComparer.Instance)
                .ThenBy(e => e.Team, SwedishNameComparer.Instance);
        }

        static bool HaveSameRank(RankedCompetitorEntry left, RankedCompetitorEntry right)
        {
            return RankingPositionService.HaveSameScoreAndMedals(
                left,
                right,
                e => e.Score,
                e => e.GoldCount,
                e => e.SilverCount,
                e => e.BronzeCount);
        }

        static IEnumerable<RankedCompetitorEntry> OrderEntriesForDisplay(
            IEnumerable<RankedCompetitorEntry> entries,
            CompetitorSortE sortBy)
        {
            return sortBy switch
            {
                CompetitorSortE.FirstName => entries
                    .OrderBy(e => e.Competitor.FirstName, SwedishNameComparer.Instance)
                    .ThenBy(e => e.Competitor.LastName, SwedishNameComparer.Instance)
                    .ThenBy(e => e.Position),
                CompetitorSortE.LastName => entries
                    .OrderBy(e => e.Competitor.LastName, SwedishNameComparer.Instance)
                    .ThenBy(e => e.Competitor.FirstName, SwedishNameComparer.Instance)
                    .ThenBy(e => e.Position),
                CompetitorSortE.Team => entries
                    .OrderBy(e => e.Team, SwedishNameComparer.Instance)
                    .ThenBy(e => e.FullName, SwedishNameComparer.Instance)
                    .ThenBy(e => e.Position),
                _ => entries
                    .OrderBy(e => e.Position)
            };
        }

        static RankedCompetitorEntry? BuildEntry(
            Competitor competitor,
            CompetitionDisciplineE discipline,
            RankingAgeClassE ageClass,
            RankingBeltGroupE beltGroup)
        {
            if (competitor.CurrentAgeClass != ageClass)
                return null;

            if (ResolveFromLatestBelt(competitor.LatestBelt) != beltGroup)
                return null;

            var relevantResults = competitor.Results
                .Where(r => discipline == CompetitionDisciplineE.Alla || r.Discipline == discipline)
                .ToList();

            if (relevantResults.Count == 0 && !competitor.IsFavorite)
                return null;

            return new RankedCompetitorEntry
            {
                Competitor = competitor,
                FullName = competitor.FullName,
                Team = competitor.Team,
                Score = relevantResults.Sum(r => r.PointsAwarded),
                Rating = relevantResults.Sum(r => r.RatingAwarded),
                ResultCount = relevantResults.Count,
                GoldCount = relevantResults.Count(r => r.Placement == 1),
                SilverCount = relevantResults.Count(r => r.Placement == 2),
                BronzeCount = relevantResults.Count(r => r.Placement == 3),
                IsFavorite = competitor.IsFavorite,
                HasResults = relevantResults.Count > 0,
            };
        }

        static RankingBeltGroupE ResolveFromLatestBelt(ChildBelt latestBelt)
        {
            return latestBelt.Level switch
            {
                <= 4 => RankingBeltGroupE.Vitt,
                <= 8 => RankingBeltGroupE.Gult,
                _ => RankingBeltGroupE.Gront,
            };
        }

        static string ToDisplayName(RankingAgeClassE ageClass)
        {
            return RankingClassFilterService.ToDisplayName(ageClass);
        }

        static string ToDisplayName(RankingBeltGroupE beltGroup)
        {
            return beltGroup switch
            {
                RankingBeltGroupE.Vitt => "Vitt bälte",
                RankingBeltGroupE.Gult => "Gult bälte",
                RankingBeltGroupE.Gront => "Grönt bälte",
                _ => beltGroup.ToString(),
            };
        }
    }
}
