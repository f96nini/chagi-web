namespace ITF_Taekwondo_Gradering.Models.Ranking;

public sealed class CompetitorBadgeAward
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string IconSource { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public DateTime AchievedDate { get; init; }
}
