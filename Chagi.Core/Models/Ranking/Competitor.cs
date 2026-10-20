namespace ITF_Taekwondo_Gradering.Models.Ranking
{
    public class Competitor
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Team { get; set; } = string.Empty;
        public bool? IsMale { get; set; }
        public bool IsFavorite { get; set; }
        public ChildBelt LatestBelt { get; set; } = ChildBelt.Kup10_1;
        public RankingAgeClassE? CurrentAgeClass { get; set; }
        public RankingAgeClassE? LatestCompetitionAgeClass { get; set; }

        public int TotalPoints =>
            Tul.Points +
            Sparring.Points +
            Specialtekniker.Points +
            Kross.Points;

        public decimal TotalRating =>
            Tul.Rating +
            Sparring.Rating +
            Specialtekniker.Rating +
            Kross.Rating;

        public DisciplineScoreSummary Tul { get; set; } = new();
        public DisciplineScoreSummary Sparring { get; set; } = new();
        public DisciplineScoreSummary Specialtekniker { get; set; } = new();
        public DisciplineScoreSummary Kross { get; set; } = new();
        public CompetitorRankingSnapshot RankingSnapshot { get; set; } = new();

        public List<CompetitionResult> Results { get; set; } = new();

        public string FullName => $"{FirstName} {LastName}".Trim();
        public string FavoriteKey => $"{FullName}|{Team}".Trim().ToLowerInvariant();
    }
}
