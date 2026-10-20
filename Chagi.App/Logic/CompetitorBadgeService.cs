using ITF_Taekwondo_Gradering.Models.Ranking;

namespace ITF_Taekwondo_Gradering.Logic;

public static class CompetitorBadgeService
{
    public static Task<IReadOnlyList<CompetitorBadgeAward>> CalculateAsync(Competitor competitor)
        => Task.Run(async () => CompetitorBadgeCalculator.Calculate(
            competitor,
            await CompetitorRepository.GetAllAsync().ConfigureAwait(false)));
}
