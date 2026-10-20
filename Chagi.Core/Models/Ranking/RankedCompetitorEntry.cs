namespace ITF_Taekwondo_Gradering.Models.Ranking
{
    public class RankedCompetitorEntry
    {
        public int Position { get; set; }
        public string PositionLabel { get; set; } = string.Empty;
        public Competitor Competitor { get; set; } = new();
        public string FullName { get; set; } = string.Empty;
        public string Team { get; set; } = string.Empty;
        public int Score { get; set; }
        public decimal Rating { get; set; }
        public int ResultCount { get; set; }
        public int GoldCount { get; set; }
        public int SilverCount { get; set; }
        public int BronzeCount { get; set; }
        public bool IsFavorite { get; set; }
        public bool HasResults { get; set; }
        public string PositionDisplayLabel => PositionLabel == "-" ? "-" : $"{PositionLabel}.";
        public string FavoriteGlyph => IsFavorite ? "★" : "☆";
    }
}
