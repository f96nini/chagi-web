namespace ITF_Taekwondo_Gradering.Models.Ranking
{
    public class CompetitionResult
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid CompetitorId { get; set; }
        public Guid CompetitionId { get; set; }

        public CompetitionDisciplineE Discipline { get; set; }

        // Zero records participation without a reported placement; it awards no medal or points.
        public int Placement { get; set; }
        public int PointsAwarded { get; set; }
        public decimal RatingAwarded { get; set; }

        public DateTime CompetitionDate { get; set; }
        public string CompetitionName { get; set; } = string.Empty;
        public string CompetitionCity { get; set; } = string.Empty;
        public string Division { get; set; } = string.Empty;
        public string BeltGroup { get; set; } = string.Empty;
        public string CategoryClass { get; set; } = string.Empty;
    }
}
