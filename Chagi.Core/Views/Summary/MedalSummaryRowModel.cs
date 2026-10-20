namespace ITF_Taekwondo_Gradering.Views;

public sealed class MedalSummaryRowModel
{
    public int GoldCount { get; init; }
    public int SilverCount { get; init; }
    public int BronzeCount { get; init; }
    public int TotalMedalCount => GoldCount + SilverCount + BronzeCount;
    public int CompetitionCount { get; init; }
    public int LeaguePosition { get; init; }
    public int PointsLeaguePosition { get; init; }
    public string TotalMedalsText => TotalMedalCount == 1 ? "1 medalj" : $"{TotalMedalCount} medaljer";
    public string CompetitionCountText => CompetitionCount == 1 ? "1 tävling" : $"{CompetitionCount} tävlingar";
    public string LeaguePositionText => $"{PositionText(LeaguePosition)} i Medaljligan";
    public string PointsLeaguePositionText => $"{PositionText(PointsLeaguePosition)} i poängligan";

    static string PositionText(int position) => position > 0 ? $"#{position}" : "–";
}
