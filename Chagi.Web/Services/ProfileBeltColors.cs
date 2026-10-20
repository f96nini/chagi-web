using ITF_Taekwondo_Gradering.Models.Ranking;

namespace Chagi.Web.Services;

public sealed record ProfileBeltColor(string CssClass, string Name, int Order);

public static class ProfileBeltColors
{
    // Results identify a competition's permitted belt colors, not a verified personal grade.
    public static IReadOnlyList<ProfileBeltColor> FromLatestClass(Competitor fighter)
    {
        var latest = fighter.Results
            .Where(result => !string.IsNullOrWhiteSpace(result.BeltGroup))
            .GroupBy(result => result.CompetitionDate.Date)
            .OrderByDescending(group => group.Key)
            .FirstOrDefault();
        if (latest == null) return [];

        var colorGroups = latest.Select(result => Parse(result.BeltGroup))
            .Where(colors => colors.Count > 0).ToList();
        if (colorGroups.Count == 0) return [];

        // A single-color class is more precise than a combined class on the same date.
        var fewestColors = colorGroups.Min(colors => colors.Count);
        return colorGroups.Where(colors => colors.Count == fewestColors)
            .SelectMany(colors => colors)
            .DistinctBy(color => color.CssClass)
            .OrderBy(color => color.Order)
            .ToArray();
    }

    static IReadOnlyList<ProfileBeltColor> Parse(string group) => group.Split('(')[0]
        .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(color => color.ToUpperInvariant() switch
        {
            "VIT" => new ProfileBeltColor("white", "Vit", 0),
            "GUL" => new ProfileBeltColor("yellow", "Gul", 1),
            "GRÖN" => new ProfileBeltColor("green", "Grön", 2),
            "BLÅ" => new ProfileBeltColor("blue", "Blå", 3),
            "RÖD" => new ProfileBeltColor("red", "Röd", 4),
            _ => null
        })
        .OfType<ProfileBeltColor>()
        .DistinctBy(color => color.CssClass)
        .ToArray();
}
