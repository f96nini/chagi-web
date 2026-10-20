namespace ITF_Taekwondo_Gradering.Models.Ranking
{
    public class CompetitorRankingSnapshot
    {
        public Guid CompetitorId { get; set; }
        public DateTime CalculatedAt { get; set; }

        public int TotalPoints { get; set; }
        public decimal TotalRating { get; set; }
        public int TotalPosition { get; set; }

        public DisciplineScoreSummary Tul { get; set; } = new();
        public DisciplineScoreSummary Sparring { get; set; } = new();
        public DisciplineScoreSummary Specialtekniker { get; set; } = new();
        public DisciplineScoreSummary Kross { get; set; } = new();
    }
}
