namespace ITF_Taekwondo_Gradering.Models.Clubs;

public sealed class ClubSummaryEntry
{
    public int Rank { get; init; }
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string ImageSource { get; init; } = Logic.ClubCatalogService.PlaceholderImage;
    public int CompetitionCount { get; init; }
    public int FighterCount { get; init; }
    public int GoldCount { get; init; }
    public int SilverCount { get; init; }
    public int BronzeCount { get; init; }

    public int TotalMedalCount => GoldCount + SilverCount + BronzeCount;
    public int TotalPoints => GoldCount * 5 + SilverCount * 3 + BronzeCount;
    public string PointsText => $"{TotalPoints} medaljpoäng";
    public string RankLabel => $"{Rank}.";

    public string CompetitionCountText => CompetitionCount == 1
        ? "1 tävling"
        : $"{CompetitionCount} tävlingar";
    public string FighterCountText => FighterCount == 1 ? "1 fighter" : $"{FighterCount} fighters";
    public string ParticipationText => $"{CompetitionCountText} · {FighterCountText}";
}
