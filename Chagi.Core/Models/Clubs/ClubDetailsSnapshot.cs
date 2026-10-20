using ITF_Taekwondo_Gradering.Models.Ranking;

namespace ITF_Taekwondo_Gradering.Models.Clubs;

public enum ClubCompetitionSortMode { Date, MedalPoints }

public sealed class ClubDetailsSnapshot
{
    public IReadOnlyList<RankedCompetitorEntry> Fighters { get; init; } = [];
    public IReadOnlyList<ClubCompetitionEntry> Competitions { get; init; } = [];
    public int TotalPoints => Fighters.Sum(fighter => fighter.Score);
}

public sealed class ClubCompetitionEntry
{
    public DateTime Date { get; init; }
    public string Name { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public int TotalPoints { get; init; }
    public int GoldCount { get; init; }
    public int SilverCount { get; init; }
    public int BronzeCount { get; init; }
    public int FighterCount { get; init; }
}
