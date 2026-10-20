using Microsoft.Extensions.Configuration;
using ITF_Taekwondo_Gradering.Logic;

namespace Chagi.Web.Services;

public sealed class PublicImages(IConfiguration configuration)
{
    // Club artwork is copied from the Chagi app into this site's static assets.
    // An explicitly configured public URL can still replace an individual logo.
    public string? Club(string id)
    {
        var configured = Safe(configuration[$"PublicImages:Clubs:{id}"]);
        if (configured != null) return configured;
        var filename = ClubCatalogService.GetAllClubs().FirstOrDefault(c => c.Id == id)?.ImageSource;
        if (string.IsNullOrWhiteSpace(filename)) return "images/club-placeholder.svg";
        if (filename == "kungalv.png") filename = "kungalv.svg";
        return $"images/clubs/{Uri.EscapeDataString(filename)}";
    }
    public string? Fighter(string id) => Safe(configuration[$"PublicImages:Fighters:{id}"]);
    static string? Safe(string? value) => Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps ? uri.AbsoluteUri : null;
}
