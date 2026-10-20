using Microsoft.Extensions.Configuration;

namespace Chagi.Web.Services;

public sealed class PublicImages(IConfiguration configuration)
{
    // Only explicitly configured, approved public images are used. The mobile
    // resource name is not assumed to be a URL or evidence of publication rights.
    public string? Club(string id) => Safe(configuration[$"PublicImages:Clubs:{id}"]);
    public string? Fighter(string id) => Safe(configuration[$"PublicImages:Fighters:{id}"]);
    static string? Safe(string? value) => Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps ? uri.AbsoluteUri : null;
}
