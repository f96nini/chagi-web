namespace ITF_Taekwondo_Gradering.Models.Ranking;

public sealed record CompetitionMatchHistory(DateTime Date, string Name, IReadOnlyList<DivisionMatch> Matches);

public enum HeadToHeadOutcome { Win, Loss, Draw, Unknown }

public sealed record HeadToHeadMeeting(
    DateTime Date, string CompetitionName, string Division, HeadToHeadOutcome Outcome,
    bool IsPlacementComparison, decimal? FighterPoints, decimal? OpponentPoints,
    decimal? FighterFlags, decimal? OpponentFlags, int? FighterPlacement, int? OpponentPlacement,
    CompetitionDisciplineE Discipline = CompetitionDisciplineE.Alla)
{
    public bool HasDetailedResult => !IsPlacementComparison &&
        (FighterPoints.HasValue || OpponentPoints.HasValue || FighterFlags.HasValue || OpponentFlags.HasValue);
}

public sealed class HeadToHeadOpponent
{
    public string Name { get; init; } = string.Empty;
    public string Team { get; init; } = string.Empty;
    public List<HeadToHeadMeeting> Meetings { get; } = [];
    public int MeetingCount => Meetings.Count;
    public int Wins => Meetings.Count(m => m.Outcome == HeadToHeadOutcome.Win);
    public int Losses => Meetings.Count(m => m.Outcome == HeadToHeadOutcome.Loss);
    public int Draws => Meetings.Count(m => m.Outcome == HeadToHeadOutcome.Draw);
    public int Unknown => Meetings.Count(m => m.Outcome == HeadToHeadOutcome.Unknown);
    public bool FighterLeads => Wins > Losses;
    public bool OpponentLeads => Losses > Wins;
}

public sealed record HeadToHeadSection(CompetitionDisciplineE Discipline, IReadOnlyList<HeadToHeadOpponent> Opponents);
