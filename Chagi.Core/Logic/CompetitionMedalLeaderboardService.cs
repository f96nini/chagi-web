using System.Globalization;
using ITF_Taekwondo_Gradering.Models.Ranking;

namespace ITF_Taekwondo_Gradering.Logic;

public static class CompetitionMedalLeaderboardService
{
    static readonly StringComparer NameComparer = StringComparer.Create(CultureInfo.GetCultureInfo("sv-SE"), true);

    public static IReadOnlyList<CompetitionMedalLeaderboardGroup> BuildGroups(IEnumerable<CompetitionDivisionEntry> divisions)
    {
        // A participant appears once, with points summed across every discipline.
        var entries = divisions.SelectMany(division => division.Results.Select(result =>
                (Result: result, division.AgeClass)))
            .GroupBy(participation => participation.Result.CompetitorId)
            .Select(results =>
            {
                var participant = results.First().Result;
                return new CompetitionMedalLeaderboardEntry
                {
                    CompetitorId = participant.CompetitorId,
                    CompetitorName = participant.CompetitorName,
                    CompetitorLastName = participant.CompetitorLastName,
                    Team = participant.Team,
                    AgeClassLabel = BuildAgeClassLabel(results.Select(result => result.AgeClass)),
                    TotalPoints = results.Sum(result => result.Result.Placement switch
                    {
                        1 => 5,
                        2 => 3,
                        3 => 1,
                        _ => 0
                    })
                };
            });

        // Dense positions: the three highest distinct scores receive trophies.
        // Medal counts never break a tie in this leaderboard.
        return entries.GroupBy(entry => entry.TotalPoints)
            .OrderByDescending(group => group.Key)
            .Select((group, index) => new CompetitionMedalLeaderboardGroup(index + 1, group.Key,
                group.OrderBy(entry => string.IsNullOrWhiteSpace(entry.CompetitorLastName)
                        ? entry.CompetitorName : entry.CompetitorLastName, NameComparer)
                    .ThenBy(entry => entry.CompetitorName, NameComparer)
                    .ThenBy(entry => entry.Team, NameComparer)))
            .ToList();
    }

    static string BuildAgeClassLabel(IEnumerable<RankingAgeClassE?> ageClasses)
    {
        var labels = ageClasses.Where(ageClass => ageClass.HasValue)
            .Select(ageClass => ageClass!.Value)
            .Distinct()
            .OrderBy(ageClass => ageClass)
            .Select(RankingClassFilterService.ToSingularDisplayName)
            .ToList();
        return labels.Count == 0 ? "Åldersklass saknas" : string.Join(", ", labels);
    }
}

public sealed class CompetitionMedalLeaderboardEntry
{
    public Guid CompetitorId { get; init; }
    public string CompetitorName { get; init; } = string.Empty;
    public string CompetitorLastName { get; init; } = string.Empty;
    public string Team { get; init; } = string.Empty;
    public string AgeClassLabel { get; init; } = string.Empty;
    public int TotalPoints { get; init; }
    public string PointsLabel => $"{TotalPoints} p";
}

public sealed class CompetitionMedalLeaderboardGroup(int position, int totalPoints,
    IEnumerable<CompetitionMedalLeaderboardEntry> entries) : List<CompetitionMedalLeaderboardEntry>(entries)
{
    public int Position { get; } = position;
    public int TotalPoints { get; } = totalPoints;
    public string PointsLabel => $"{TotalPoints} poäng";
    public string Title => Position switch
    {
        1 => "Första plats",
        2 => "Andra plats",
        3 => "Tredje plats",
        4 => "Övriga deltagare",
        _ => string.Empty
    };
    public bool HasTitle => Position <= 4;
    public bool HasTrophy => Position <= 3;
    public string? TrophyImageSource => Position switch
    {
        1 => "trophy_gold.png",
        2 => "trophy_silver.png",
        3 => "trophy_bronze.png",
        _ => null
    };
}
