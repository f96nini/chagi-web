using System.Globalization;

namespace ITF_Taekwondo_Gradering.Models.Ranking;

public sealed class DivisionMatch
{
    public int CategoryId { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Discipline { get; set; } = string.Empty;
    public string Ring { get; set; } = string.Empty;
    public int MatchId { get; set; }
    public int MatchNumber { get; set; }
    public int Finished { get; set; }
    public int? RedId { get; set; }
    public string? RedParticipant { get; set; }
    public decimal? RedPoints { get; set; }
    public decimal? RedFlags { get; set; }
    public int? BlueId { get; set; }
    public string? BlueParticipant { get; set; }
    public decimal? BluePoints { get; set; }
    public decimal? BlueFlags { get; set; }
    public int? ExtraMatchId { get; set; }
    public string? StartAt { get; set; }
    public string? Group { get; set; }
    public int? WinnerId { get; set; }
    public string? Winner { get; set; }

    public bool IsIndividualAttempt => Discipline is "SPEC_TECH" or "POWER_BREAK";
    public bool HasBlueParticipant => BlueId.HasValue && !string.IsNullOrWhiteSpace(BlueParticipant) && BlueParticipant != "?";
    // Individual-attempt records currently contain no technique values, only registrations.
    public bool HasDetailedData => !IsIndividualAttempt && RedId.HasValue && HasBlueParticipant &&
        (RedPoints.HasValue || BluePoints.HasValue || RedFlags.HasValue || BlueFlags.HasValue);
    public DateTime? StartTime => DateTime.TryParseExact(StartAt, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture,
        DateTimeStyles.None, out var time) ? time : null;

    public string Outcome => Finished != 1 ? "Ej avslutad"
        : !string.IsNullOrWhiteSpace(Winner) ? $"Vinnare: {Winner}"
        : HasBlueParticipant && RedPoints.HasValue && BluePoints.HasValue && RedPoints == BluePoints ? "Oavgjort"
        : "Avslutad";

    public static string FormatScore(decimal? score) => score?.ToString("0.##", CultureInfo.GetCultureInfo("sv-SE")) ?? "–";
}

public sealed class MatchDataIndexEntry
{
    public DateTime CompetitionDate { get; set; }
    public string CompetitionName { get; set; } = string.Empty;
    public string File { get; set; } = string.Empty;
}
