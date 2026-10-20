using System.Globalization;
using ITF_Taekwondo_Gradering.Models.Ranking;

namespace ITF_Taekwondo_Gradering.Logic;

public static class CompetitionCatalogRepository
{
    static List<CompetitionCatalogEntry>? _cache;
    static int _cacheVersion = -1;
    static readonly SemaphoreSlim CacheLock = new(1, 1);

    public static Task<List<CompetitionCatalogEntry>> GetCatalogAsync()
        => Task.Run(() => GetCatalogCoreAsync());

    static async Task<List<CompetitionCatalogEntry>> GetCatalogCoreAsync()
    {
        var repositoryVersion = CompetitorRepository.GetChangeVersion();
        if (_cache != null && _cacheVersion == repositoryVersion)
            return _cache;

        await CacheLock.WaitAsync().ConfigureAwait(false);
        try
        {
            repositoryVersion = CompetitorRepository.GetChangeVersion();
            if (_cache != null && _cacheVersion == repositoryVersion)
                return _cache;

            var competitors = await CompetitorRepository.GetAllAsync().ConfigureAwait(false);
            _cache = CompetitionCatalogService.BuildCatalog(competitors);
            _cacheVersion = repositoryVersion;
            return _cache;
        }
        finally
        {
            CacheLock.Release();
        }
    }

}
