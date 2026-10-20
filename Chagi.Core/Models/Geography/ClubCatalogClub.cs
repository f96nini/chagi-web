namespace ITF_Taekwondo_Gradering.Models.Geography;

public sealed class ClubCatalogClub
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? CityId { get; init; }
    public string? ImageSource { get; init; }
    public IReadOnlyList<string> Aliases { get; init; } = Array.Empty<string>();
}
