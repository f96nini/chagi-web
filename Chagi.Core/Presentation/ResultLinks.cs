using System.Text;
using System.Globalization;
using System.Security.Cryptography;
using ITF_Taekwondo_Gradering.Logic;
using ITF_Taekwondo_Gradering.Models.Ranking;

namespace Chagi.Core.Presentation;

public static class ResultLinks
{
    // Mobile IDs are generated during import. Public URLs must survive re-imports.
    // Keep the name and club readable in Web Analytics paths while the suffix avoids slug collisions.
    public static string FighterId(Competitor fighter) =>
        $"{Slug(fighter.FullName)}--{Slug(fighter.Team)}-{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fighter.FavoriteKey)))[..12].ToLowerInvariant()}";
    public static bool MatchesFighterId(Competitor fighter, string id) =>
        FighterId(fighter) == id || Encode(fighter.FavoriteKey) == id;
    public static string CompetitionId(CompetitionCatalogEntry competition) => Encode(competition.Key);
    public static string DivisionId(string division) => Encode(division.Trim().Normalize(NormalizationForm.FormC).ToUpperInvariant());
    static string Encode(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value))
        .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    static string Slug(string value)
    {
        var decomposed = value.Trim().Normalize(NormalizationForm.FormD);
        var result = new StringBuilder();
        var separator = false;
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark) continue;
            var lower = char.ToLowerInvariant(character);
            if (lower is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                if (separator && result.Length > 0) result.Append('-');
                result.Append(lower);
                separator = false;
            }
            else separator = true;
        }
        return result.Length == 0 ? "okand" : result.ToString();
    }
}
