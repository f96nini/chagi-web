using ITF_Taekwondo_Gradering.Models.Ranking;

namespace ITF_Taekwondo_Gradering.Logic;

public static class CompetitorBadgeCalculator
{
    static readonly IReadOnlyList<BadgeDefinition> Definitions =
    [
        new("first-medal", "Första medaljen", "bronze_large.png", context => context.DateWhenMedalCountReached(1)),
        new("first-gold", "Första guldet", "gold_large.png", context => context.DateWhenPlacementCountReached(1, 1)),
        new("three-competitions", "Medalj i 3 tävlingar", "silver_large.png", context => context.DateWhenCompetitionCountReached(3)),
        new("ten-medals", "10 medaljer", "bronze_large.png", context => context.DateWhenMedalCountReached(10)),
        new("medal-horder-level1", "Medal Horder Level 1", "medal_horder1.png", context => context.DateWhenMedalCountReached(10), "Har minst 10 medaljer"),
        new("medal-horder-level2", "Medal Horder Level 2", "medal_horder2.png", context => context.DateWhenMedalCountReached(20), "Har minst 20 medaljer"),
        new("medal-horder-level3", "Medal Horder Level 3", "medal_horder3.png", context => context.DateWhenMedalCountReached(30), "Har minst 30 medaljer"),
        new("medal-horder-level4", "Medal Horder Level 4", "medal_horder4.png", context => context.DateWhenMedalCountReached(40), "Har minst 40 medaljer"),
        new("medal-horder-level5", "Medal Horder Level 5", "medal_horder5.png", context => context.DateWhenMedalCountReached(50), "Har minst 50 medaljer"),
        new("three-golds", "3 guldmedaljer", "gold_large.png", context => context.DateWhenPlacementCountReached(1, 3)),
        new("hat-trick", "Hat-Trick", "gold_large.png", context => context.DateWhenGoldHatTrickAchieved(), "3 guld på samma tävling"),
        new("lonely-legend", "Lonely Legend", "lonely_legend.png", context => context.DateWhenPlacementMissingPlacementAchieved(1, 2), "Ensam vinnare"),
        new("silver-by-signup", "Silver by Signup", "silver_by_signup.png", context => context.DateWhenSilverWithoutBronzeAchieved(), "Silvermedalj i en grupp med två deltagare"),
        new("the-other-guy-lost", "The Other Guy Lost", "the_other_guy_lost.png", context => context.DateWhenGoldWithSilverButNoBronzeAchieved(), "Guldmedalj i en grupp med två deltagare"),
        new("tul-medal", "Medalj i tul", "tul.png", context => context.FirstMedalDateInDiscipline(CompetitionDisciplineE.Tul)),
        new("sparring-medal", "Medalj i sparring", "sparring.png", context => context.FirstMedalDateInDiscipline(CompetitionDisciplineE.Sparring)),
        new("spec-medal", "Medalj i spec", "spec.png", context => context.FirstMedalDateInDiscipline(CompetitionDisciplineE.Specialtekniker)),
        new("kross-medal", "Medalj i kross", "kross.png", context => context.FirstMedalDateInDiscipline(CompetitionDisciplineE.Kross)),
        new("tul-master", "Tul King", "tul_king.png", context => context.DateWhenTopTenReached(CompetitionDisciplineE.Tul), "Topp 10 i Tul"),
        new("top-fighter", "Top Fighter", "sparring_king.png", context => context.DateWhenTopTenReached(CompetitionDisciplineE.Sparring), "Topp 10 i Sparring"),
        new("horse-kick", "Horse Kick", "kross_king.png", context => context.DateWhenTopTenReached(CompetitionDisciplineE.Kross), "Topp 10 i Kross"),
        new("birdie", "Birdie", "spec_king.png", context => context.DateWhenTopTenReached(CompetitionDisciplineE.Specialtekniker), "Topp 10 i Spec"),
        new("all-disciplines", "Pallplats i alla discipliner", "gold_large.png", context => context.DateWhenAllDisciplinesCompleted()),

        new("goteborg-1", "Räkmacka Rookie", "gbg_silver.png", context => context.FirstCompetitionDateInCity("Göteborg"), "Har tagit medalj i Göteborg"),
        new("goteborg-3", "Göteborg Grand Kick", "gbg_gold.png", context => context.DateWhenCompetitionCountReachedInCity("Göteborg", 3), "Har tagit medalj minst 3 tävlingar i Göteborg"),

        new("stockholm-1", "Capital Fighter", "sthlm_silver.png", context => context.FirstCompetitionDateInCity("Stockholm"), "Har tagit medalj i Stockholm"),
        new("stockholm-3", "Stockholm Spin Kick", "sthlm_gold.png", context => context.DateWhenCompetitionCountReachedInCity("Stockholm", 3), "Har tagit medalj minst 3 tävlingar i Stockholm"),

        new("ljungby-1", "Småland Low Kick", "ljungby_silver.png", context => context.FirstCompetitionDateInCity("Ljungby"), "Har tagit medalj i Ljungby"),
        new("ljungby-3", "Ljungby Lightning", "ljungby_gold.png", context => context.DateWhenCompetitionCountReachedInCity("Ljungby", 3), "Har tagit medalj minst 3 tävlingar i Ljungby"),

        new("skovde-1", "Valhalla Kickoff", "skovde_silver.png", context => context.FirstCompetitionDateInCity("Skövde"), "Har tagit medalj i Skövde"),
        new("skovde-3", "Viking Champion", "skovde_gold.png", context => context.DateWhenCompetitionCountReachedInCity("Skövde", 3), "Har tagit medalj minst 3 tävlingar i Skövde"),

        new("stenungsund-1", "Bridge Kickoff", "stenungsund_silver.png", context => context.FirstCompetitionDateInCity("Stenungsund"), "Har tagit medalj i Stenungsund"),
        new("stenungsund-3", "West Coast Legend", "stenungsund_gold.png", context => context.DateWhenCompetitionCountReachedInCity("Stenungsund", 3), "Har tagit medalj minst 3 tävlingar i Stenungsund"),
    ];

    public static IReadOnlyList<CompetitorBadgeAward> Calculate(Competitor competitor, IEnumerable<Competitor> allCompetitors)
    {
        var context = new BadgeEvaluationContext(competitor, allCompetitors);

        return Definitions
            .Select(definition =>
            {
                var achievedDate = definition.GetAchievedDate(context);
                return achievedDate.HasValue
                    ? new CompetitorBadgeAward
                    {
                        Id = definition.Id,
                        Title = definition.Title,
                        IconSource = definition.IconSource,
                        Description = definition.Description,
                        AchievedDate = achievedDate.Value
                    }
                    : null;
            })
            .Where(badge => badge != null)
            .Cast<CompetitorBadgeAward>()
            .OrderByDescending(badge => badge.AchievedDate)
            .ThenBy(badge => badge.Title, SwedishNameComparer.Instance)
            .ToList();
    }

    sealed class BadgeEvaluationContext(Competitor competitor, IEnumerable<Competitor> allCompetitors)
    {
        readonly List<CompetitionResult> _allResults = competitor.Results
            .OrderBy(result => result.CompetitionDate)
            .ThenBy(result => result.CompetitionName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(result => result.CompetitionCity, StringComparer.OrdinalIgnoreCase)
            .ThenBy(result => result.Discipline)
            .ThenBy(result => result.Placement)
            .ToList();

        readonly Dictionary<string, HashSet<int>> _placementsByDivisionKey = allCompetitors
            .SelectMany(static item => item.Results)
            .Where(static result => result.Placement > 0)
            .GroupBy(GetDivisionKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                static group => group.Key,
                static group => group.Select(result => result.Placement).ToHashSet(),
                StringComparer.OrdinalIgnoreCase);

        readonly List<CompetitionResult> _medalResults = competitor.Results
            .Where(result => result.Placement is >= 1 and <= 3)
            .OrderBy(result => result.CompetitionDate)
            .ThenBy(result => result.CompetitionName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(result => result.CompetitionCity, StringComparer.OrdinalIgnoreCase)
            .ThenBy(result => result.Discipline)
            .ThenBy(result => result.Placement)
            .ToList();

        public DateTime? DateWhenMedalCountReached(int threshold)
        {
            if (threshold <= 0)
                return null;

            for (var i = 0; i < _medalResults.Count; i++)
            {
                if (i + 1 >= threshold)
                    return _medalResults[i].CompetitionDate;
            }

            return null;
        }

        public DateTime? DateWhenPlacementCountReached(int placement, int threshold)
        {
            if (threshold <= 0)
                return null;

            var count = 0;
            foreach (var result in _medalResults)
            {
                if (result.Placement != placement)
                    continue;

                count++;
                if (count >= threshold)
                    return result.CompetitionDate;
            }

            return null;
        }

        public DateTime? DateWhenCompetitionCountReached(int threshold)
        {
            if (threshold <= 0)
                return null;

            var seenCompetitions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var result in _medalResults)
            {
                if (!seenCompetitions.Add(GetCompetitionKey(result)))
                    continue;

                if (seenCompetitions.Count >= threshold)
                    return result.CompetitionDate;
            }

            return null;
        }

        public DateTime? FirstMedalDateInDiscipline(CompetitionDisciplineE discipline)
        {
            return _medalResults
                .Where(result => result.Discipline == discipline)
                .Select(result => (DateTime?)result.CompetitionDate)
                .FirstOrDefault();
        }

        public DateTime? DateWhenGoldHatTrickAchieved()
        {
            return _medalResults
                .Where(result => result.Placement == 1)
                .GroupBy(GetCompetitionKey)
                .OrderBy(group => group.Min(result => result.CompetitionDate))
                .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() >= 3)
                .Select(group => (DateTime?)group.Min(result => result.CompetitionDate))
                .FirstOrDefault();
        }

        public DateTime? DateWhenPlacementMissingPlacementAchieved(int placement, int missingPlacement)
        {
            return _medalResults
                .Where(result => result.Placement == placement)
                .Where(result => !HasPlacement(result, missingPlacement))
                .Select(result => (DateTime?)result.CompetitionDate)
                .FirstOrDefault();
        }

        public DateTime? DateWhenSilverWithoutBronzeAchieved()
        {
            return _medalResults
                .Where(result => result.Placement == 2)
                .Where(result => HasPlacement(result, 1) && !HasPlacement(result, 3))
                .Select(result => (DateTime?)result.CompetitionDate)
                .FirstOrDefault();
        }

        public DateTime? DateWhenGoldWithSilverButNoBronzeAchieved()
        {
            return _medalResults
                .Where(result => result.Placement == 1)
                .Where(result => HasPlacement(result, 2) && !HasPlacement(result, 3))
                .Select(result => (DateTime?)result.CompetitionDate)
                .FirstOrDefault();
        }

        public DateTime? DateWhenTopTenReached(CompetitionDisciplineE discipline)
        {
            var position = discipline switch
            {
                CompetitionDisciplineE.Tul => competitor.RankingSnapshot.Tul.Position,
                CompetitionDisciplineE.Sparring => competitor.RankingSnapshot.Sparring.Position,
                CompetitionDisciplineE.Specialtekniker => competitor.RankingSnapshot.Specialtekniker.Position,
                CompetitionDisciplineE.Kross => competitor.RankingSnapshot.Kross.Position,
                _ => 0
            };

            if (position is <= 0 or > 10)
                return null;

            return _allResults
                .Select(result => (DateTime?)result.CompetitionDate)
                .LastOrDefault();
        }

        public DateTime? FirstCompetitionDateInCity(string city)
        {
            if (string.IsNullOrWhiteSpace(city))
                return null;

            var normalizedCity = city.Trim();

            return _allResults
                .Where(result => string.Equals(result.CompetitionCity?.Trim(), normalizedCity, StringComparison.OrdinalIgnoreCase))
                .Select(result => (DateTime?)result.CompetitionDate)
                .FirstOrDefault();
        }

        public DateTime? DateWhenCompetitionCountReachedInCity(string city, int threshold)
        {
            if (threshold <= 0 || string.IsNullOrWhiteSpace(city))
                return null;

            var normalizedCity = city.Trim();
            var seenCompetitions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var result in _allResults.Where(result =>
                         string.Equals(result.CompetitionCity?.Trim(), normalizedCity, StringComparison.OrdinalIgnoreCase)))
            {
                if (!seenCompetitions.Add(GetCompetitionKey(result)))
                    continue;

                if (seenCompetitions.Count >= threshold)
                    return result.CompetitionDate;
            }

            return null;
        }

        public DateTime? DateWhenAllDisciplinesCompleted()
        {
            var firstTul = FirstMedalDateInDiscipline(CompetitionDisciplineE.Tul);
            var firstSparring = FirstMedalDateInDiscipline(CompetitionDisciplineE.Sparring);
            var firstSpec = FirstMedalDateInDiscipline(CompetitionDisciplineE.Specialtekniker);
            var firstKross = FirstMedalDateInDiscipline(CompetitionDisciplineE.Kross);

            if (!firstTul.HasValue || !firstSparring.HasValue || !firstSpec.HasValue || !firstKross.HasValue)
                return null;

            return new[] { firstTul.Value, firstSparring.Value, firstSpec.Value, firstKross.Value }.Max();
        }

        static string GetCompetitionKey(CompetitionResult result)
        {
            return string.Join("|",
                result.CompetitionDate.ToString("yyyy-MM-dd"),
                result.CompetitionName ?? string.Empty,
                result.CompetitionCity ?? string.Empty);
        }

        static string GetDivisionKey(CompetitionResult result)
        {
            return string.Join("|",
                GetCompetitionKey(result),
                result.Division ?? string.Empty);
        }

        bool HasPlacement(CompetitionResult result, int placement)
        {
            return _placementsByDivisionKey.TryGetValue(GetDivisionKey(result), out var placements) &&
                   placements.Contains(placement);
        }
    }

    sealed class BadgeDefinition(
        string id,
        string title,
        string iconSource,
        Func<BadgeEvaluationContext, DateTime?> getAchievedDate,
        string description = "")
    {
        public string Id { get; } = id;
        public string Title { get; } = title;
        public string IconSource { get; } = iconSource;
        public Func<BadgeEvaluationContext, DateTime?> GetAchievedDate { get; } = getAchievedDate;
        public string Description { get; } = description;
    }
}
