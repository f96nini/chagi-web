using ITF_Taekwondo_Gradering.Models.Ranking;

namespace ITF_Taekwondo_Gradering.Logic;

public static class HeadToHeadService
{
    public static Task<IReadOnlyList<HeadToHeadSection>> GetAsync(Competitor fighter)
        => Task.Run(async () => HeadToHeadCalculator.Build(fighter,
            await CompetitionMatchService.GetHistoryAsync().ConfigureAwait(false),
            await CompetitorRepository.GetAllAsync().ConfigureAwait(false)));

    public static IReadOnlyList<HeadToHeadSection> Build(Competitor fighter,
        IEnumerable<CompetitionMatchHistory> history, IEnumerable<Competitor> competitors)
        => HeadToHeadCalculator.Build(fighter, history, competitors);

    public static IReadOnlyList<HeadToHeadOpponent> GroupByOpponent(IEnumerable<HeadToHeadSection> sections)
        => HeadToHeadCalculator.GroupByOpponent(sections);
}
