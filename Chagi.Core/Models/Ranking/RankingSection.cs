namespace ITF_Taekwondo_Gradering.Models.Ranking
{
    public class RankingSection
    {
        public RankingAgeClassE AgeClass { get; set; }
        public RankingBeltGroupE BeltGroup { get; set; }
        public string Title { get; set; } = string.Empty;
        public List<RankedCompetitorEntry> Entries { get; set; } = new();
    }
}
