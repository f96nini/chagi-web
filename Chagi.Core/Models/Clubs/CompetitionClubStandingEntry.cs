namespace ITF_Taekwondo_Gradering.Models.Clubs;

public sealed class CompetitionClubStandingEntry
{
    public int Rank { get; init; }
    public string ClubName { get; init; } = string.Empty;
    public int GoldCount { get; init; }
    public int SilverCount { get; init; }
    public int BronzeCount { get; init; }
    public int TotalPoints { get; init; }
}
