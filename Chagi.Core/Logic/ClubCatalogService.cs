using System.Globalization;
using System.Text;
using ITF_Taekwondo_Gradering.Models.Geography;

namespace ITF_Taekwondo_Gradering.Logic;

public static class ClubCatalogService
{
    public const string PlaceholderImage = "club_placeholder.png";
    static readonly RegionDefinition[] RegionDefinitions =
    [
        new("vastkusten", "Västkusten"),
        new("stockholm", "Stockholm"),
        new("smaland", "Småland"),
        new("skane", "Skåne")
    ];

    static readonly CityDefinition[] CityDefinitions =
    [
        new("ahlafors", "Ahlafors", "vastkusten"),
        new("billdal", "Billdal", "vastkusten"),
        new("bohus", "Bohus", "vastkusten"),
        new("floda", "Floda", "vastkusten"),
        new("frolunda", "Frölunda", "vastkusten"),
        new("goteborg", "Göteborg", "vastkusten"),
        new("hisingen", "Hisingen", "vastkusten"),
        new("kungsbacka", "Kungsbacka", "vastkusten"),
        new("kungalv", "Kungälv", "vastkusten"),
        new("karra", "Kärra", "vastkusten"),
        new("lindome", "Lindome", "vastkusten"),
        new("molnlycke", "Mölnlycke", "vastkusten"),
        new("ockero", "Öckerö", "vastkusten"),
        new("partille", "Partille", "vastkusten"),
        new("skara", "Skara", "vastkusten"),
        new("skovde", "Skövde", "vastkusten"),
        new("stenungsund", "Stenungsund", "vastkusten"),
        new("torslanda", "Torslanda", "vastkusten"),
        new("trollhattan", "Trollhättan", "vastkusten"),
        new("gnesta", "Gnesta", "stockholm"),
        new("lidingo", "Lidingö", "stockholm"),
        new("sodermalm", "Södermalm", "stockholm"),
        new("sodertalje", "Södertälje", "stockholm"),
        new("stockholm-city", "Stockholm", "stockholm"),
        new("tyreso", "Tyresö", "stockholm"),
        new("jonkoping", "Jönköping", "smaland"),
        new("ljungby", "Ljungby", "smaland"),
        new("eskilstuna", "Eskilstuna", null),
        new("linkoping", "Linköping", null)
    ];

    // MAUI images use flat resource names; SVG source files are published as PNG.
    static readonly ClubDefinition[] ClubDefinitions =
    [
        new("ahlafors", "Ahlafors", "ahlafors", "ahlafors.jpg", ["Ahlafors"]),
        new("gbg-kampsport", "GBG Kampsport", "goteborg", "gbgkampsport.png", ["GBG Kampsport", "Göteborg Kampsport", "Göteborg Kampsportsförening", "Billdal"]),
        new("bohus", "Bohus", "bohus", "bohus.png", ["Bohus"]),
        new("bohuslan", "Bohuslän", "bohus", "bohus.png", ["Bohuslän", "Bohuslan"]),
        new("eskilstuna", "Eskilstuna", "eskilstuna", "eskilstuna.png", ["Eskilstuna"]),
        new("floda", "Floda", "floda", null, ["Floda"]),
        new("frolunda", "Frölunda", "frolunda", "frolunda.png", ["Frölunda", "Frolunda"]),
        new("gnesta", "Gnesta", "gnesta", "gnesta.png", ["Gnesta"]),
        new("goteborg-fighter", "Göteborg Fighter", "goteborg", "goteborgfighter.jpg", ["Göteborg Fighter", "Goteborg Fighter"]),
        new("goteborg-phoenix", "Göteborg Phoenix", "goteborg", null, ["Göteborg Phoenix", "Goteborg Phoenix"]),
        new("goteborg-gta", "Göteborg GTA", "goteborg", "goteborggta.jpg", ["Göteborg GTA", "Goteborg GTA", "GTA"]),
        new("hisingen", "Hisingen", "hisingen", "hisingen.jpg", ["Hisingen"]),
        new("jonkoping", "Jönköping", "jonkoping", "jonkoping.png", ["Jönköping", "Jonkoping"]),
        new("kungsbacka", "Kungsbacka", "kungsbacka", "kungsbacka.png", ["Kungsbacka"]),
        new("kungalv", "Kungälv", "kungalv", "kungalv.png", ["Kungälv", "Kungalv"]),
        new("karra", "Kärra", "karra", "karra.png", ["Kärra", "Karra"]),
        new("lidingo", "Lidingö", "lidingo", "lidingo.png", ["Lidingö", "Lidingo"]),
        new("lindome", "Lindome", "lindome", "lindome.png", ["Lindome"]),
        new("linkoping", "Linköping", "linkoping", "linkoping.png", ["Linköping", "Linkoping"]),
        new("ljungby", "Ljungby", "ljungby", "ljungby.png", ["Ljungby"]),
        new("mariestad", "Mariestad", null, "mariestad.png", ["Mariestad"]),
        new("molnlycke", "Mölnlycke", "molnlycke", "molnlycke.png", ["Mölnlycke", "Molnlycke"]),
        new("next-level", "Next Level", null, null, ["Next Level"]),
        new("ockero", "Öckerö", "ockero", "ockero.png", ["Öckerö", "Ockero"]),
        new("partille", "Partille", "partille", "partille.png", ["Partille"]),
        new("skara", "Skara", "skara", null, ["Skara"]),
        new("skovde", "Skövde", "skovde", "skovde.png", ["Skövde", "Skovde"]),
        new("stenungsund", "Stenungsund", "stenungsund", "stenungsund.png", ["Stenungsund"]),
        new("stockholm", "Stockholm", "stockholm-city", "stockholm.png", ["Stockholm"]),
        new("stockholm-impact", "Stockholm Impact", "stockholm-city", "impact.png", ["Stockholm Impact", "Impact"]),
        new("sodermalm", "Södermalm", "sodermalm", "sodermalm.png", ["Södermalm", "Sodermalm"]),
        new("sodertalje", "Södertälje", "sodertalje", "sodertalje.png", ["Södertälje", "Sodertalje"]),
        new("strangnas", "Strängnäs", null, "strangnas.png", ["Strängnäs", "Strangnas"]),
        new("tkd-akademin", "TKD Akademin", "stockholm-city", "tkdakademin.png", ["TKD Akademin", "TKDA", "Akademin"]),
        new("torslanda", "Torslanda", "torslanda", "torslanda.png", ["Torslanda"]),
        new("trollhattan", "Trollhättan", "trollhattan", "trollhattan.png", ["Trollhättan", "Trollhattan"]),
        new("tyreso", "Tyresö", "tyreso", "tyreso.png", ["Tyresö", "Tyreso"])
    ];

    static readonly ClubDefinition RenamedClub = ClubDefinitions.Single(club => club.Id == "gbg-kampsport");
    static readonly HashSet<string> RenamedClubAliases = RenamedClub.Aliases.Select(Normalize).ToHashSet(StringComparer.Ordinal);

    static readonly IReadOnlyDictionary<string, CityDefinition> CityById = CityDefinitions.ToDictionary(city => city.Id);

    static readonly IReadOnlyList<ClubCatalogClub> AllClubs = ClubDefinitions
        .Select(definition => new ClubCatalogClub
        {
            Id = definition.Id,
            Name = definition.Name,
            CityId = definition.CityId,
            ImageSource = definition.ImageSource,
            Aliases = definition.Aliases
        })
        .ToList();

    static readonly IReadOnlyList<ClubCatalogCity> AllCities = CityDefinitions
        .Select(city => new ClubCatalogCity
        {
            Id = city.Id,
            Name = city.Name,
            RegionId = city.RegionId,
            Clubs = AllClubs
                .Where(club => club.CityId == city.Id)
                .OrderBy(club => club.Name, SwedishNameComparer.Instance)
                .ToList()
        })
        .ToList();

    static readonly IReadOnlyList<ClubCatalogRegion> Regions = RegionDefinitions
        .Select(region => new ClubCatalogRegion
        {
            Id = region.Id,
            Name = region.Name,
            Cities = AllCities
                .Where(city => city.RegionId == region.Id)
                .OrderBy(city => city.Name, SwedishNameComparer.Instance)
                .ToList()
        })
        .ToList();

    public static IReadOnlyList<ClubCatalogRegion> GetRegions() => Regions;

    public static IReadOnlyList<ClubCatalogCity> GetAllCities() => AllCities;

    public static IReadOnlyList<ClubCatalogClub> GetAllClubs() => AllClubs;

    // The catalog supplies known aliases and logos; a club in the results does not
    // need a catalog entry to have statistics and a working details page.
    public static ClubCatalogClub? ResolveClub(string? clubName)
    {
        if (string.IsNullOrWhiteSpace(clubName)) return null;
        var name = NormalizeClubName(clubName).Normalize(NormalizationForm.FormC);
        var normalized = Normalize(name);
        var known = AllClubs.FirstOrDefault(club => club.Aliases.Any(alias => Normalize(alias) == normalized));
        return known ?? new ClubCatalogClub
        {
            Id = $"results:{name.ToUpperInvariant()}", Name = name, Aliases = [name]
        };
    }

    // Billdal and the Göteborg names belong to the same club, including historical results.
    public static string NormalizeClubName(string clubName)
    {
        // Identity merging requires a registered alias, not the catalog's fuzzy display search.
        return RenamedClubAliases.Contains(Normalize(clubName)) ? RenamedClub.Name : clubName.Trim();
    }

    public static string? NormalizeParticipant(string? participant)
    {
        if (string.IsNullOrWhiteSpace(participant)) return participant;
        var parts = participant.Split(" - ", 2, StringSplitOptions.TrimEntries);
        return parts.Length == 2 ? $"{parts[0]} - {NormalizeClubName(parts[1])}" : participant;
    }

    public static ClubCatalogClub? FindClub(string? clubName)
    {
        var normalized = Normalize(clubName);
        if (string.IsNullOrWhiteSpace(normalized))
            return null;

        ClubCatalogClub? bestMatch = null;
        var bestAliasLength = -1;

        foreach (var club in AllClubs)
        {
            foreach (var alias in club.Aliases)
            {
                var normalizedAlias = Normalize(alias);
                if (string.IsNullOrWhiteSpace(normalizedAlias) || !normalized.Contains(normalizedAlias, StringComparison.Ordinal))
                    continue;

                if (normalizedAlias.Length <= bestAliasLength)
                    continue;

                bestMatch = club;
                bestAliasLength = normalizedAlias.Length;
            }
        }

        return bestMatch;
    }

    public static ClubCatalogCity? FindCityForClub(string? clubName)
    {
        var club = FindClub(clubName);
        return club?.CityId is { } cityId
            ? AllCities.FirstOrDefault(city => city.Id == cityId)
            : null;
    }

    public static ClubCatalogRegion? FindRegionForClub(string? clubName)
    {
        var city = FindCityForClub(clubName);
        return city?.RegionId is { } regionId
            ? Regions.FirstOrDefault(region => region.Id == regionId)
            : null;
    }

    public static string? ResolveClubImage(string? clubName)
    {
        return FindClub(clubName)?.ImageSource;
    }

    static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var normalized = value.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();

        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark &&
                (char.IsLetterOrDigit(ch) || ch == ' '))
            {
                builder.Append(ch);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC).Trim();
    }

    sealed class RegionDefinition(string id, string name)
    {
        public string Id { get; } = id;
        public string Name { get; } = name;
    }

    sealed class CityDefinition(string id, string name, string? regionId)
    {
        public string Id { get; } = id;
        public string Name { get; } = name;
        public string? RegionId { get; } = regionId;
    }

    sealed class ClubDefinition(string id, string name, string? cityId, string? imageSource, IReadOnlyList<string> aliases)
    {
        public string Id { get; } = id;
        public string Name { get; } = name;
        public string? CityId { get; } = cityId;
        public string? ImageSource { get; } = imageSource;
        public IReadOnlyList<string> Aliases { get; } = aliases;
    }
}
