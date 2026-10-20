using System.Globalization;
using ITF_Taekwondo_Gradering.Models.Clubs;
using ITF_Taekwondo_Gradering.Models.Geography;
using ITF_Taekwondo_Gradering.Models.Ranking;

namespace ITF_Taekwondo_Gradering.Logic;

public static class ClubStatisticsRepository
{
    public static Task<IReadOnlyList<ClubSummaryEntry>> GetClubSummariesAsync()
        => Task.Run(() => GetClubSummariesCoreAsync());

    static async Task<IReadOnlyList<ClubSummaryEntry>> GetClubSummariesCoreAsync()
    {
        var competitors = await CompetitorRepository.GetAllAsync().ConfigureAwait(false);
        return ClubStatisticsService.BuildClubSummaries(competitors);
    }

}
