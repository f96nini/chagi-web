namespace ITF_Taekwondo_Gradering.Models.Geography;

public sealed class ClubCatalogCity
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? RegionId { get; init; }
    public IReadOnlyList<ClubCatalogClub> Clubs { get; init; } = Array.Empty<ClubCatalogClub>();
}
