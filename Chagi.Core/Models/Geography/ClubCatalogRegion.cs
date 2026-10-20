namespace ITF_Taekwondo_Gradering.Models.Geography;

public sealed class ClubCatalogRegion
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public IReadOnlyList<ClubCatalogCity> Cities { get; init; } = Array.Empty<ClubCatalogCity>();
}
