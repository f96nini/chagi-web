namespace ITF_Taekwondo_Gradering.Models.Ranking
{
    public class Competition
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public string Name { get; set; } = string.Empty;
        public DateTime Date { get; set; }

        public string City { get; set; } = string.Empty;
        public string Arena { get; set; } = string.Empty;

        public List<CompetitionResult> Results { get; set; } = new();
    }
}
