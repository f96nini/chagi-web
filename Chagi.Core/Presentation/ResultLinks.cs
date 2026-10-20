using System.Text;
using ITF_Taekwondo_Gradering.Logic;
using ITF_Taekwondo_Gradering.Models.Ranking;

namespace Chagi.Core.Presentation;

public static class ResultLinks
{
    // Mobile IDs are generated during import. Public URLs must survive re-imports.
    public static string FighterId(Competitor fighter) => Encode(fighter.FavoriteKey);
    public static string CompetitionId(CompetitionCatalogEntry competition) => Encode(competition.Key);
    static string Encode(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value))
        .TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
