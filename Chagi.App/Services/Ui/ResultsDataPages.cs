using ITF_Taekwondo_Gradering.Logic;
using ITF_Taekwondo_Gradering.Models.Ranking;

namespace ITF_Taekwondo_Gradering;

public partial class CompetitorsPage : IResultsDataPage
{
    public Task InvalidateDataAsync()
    {
        _lifetime.Activate();
        _hasLoaded = false;
        _isRefreshing = false;
        return Task.CompletedTask;
    }
}
public partial class AllCompetitorsPage : IResultsDataPage
{
    public Task InvalidateDataAsync()
    {
        _lifetime.Activate();
        _competitors.Clear();
        _isRefreshing = false;
        return Task.CompletedTask;
    }
}
public partial class CompetitionsPage : IResultsDataPage
{
    public Task InvalidateDataAsync()
    {
        _lifetime.Activate();
        _hasLoaded = false;
        _isLoading = false;
        return Task.CompletedTask;
    }
}
public partial class ClubsPage : IResultsDataPage
{
    public Task InvalidateDataAsync()
    {
        _hasLoaded = false;
        return Task.CompletedTask;
    }
}
public partial class CompetitorSearchPage : IResultsDataPage
{
    public Task InvalidateDataAsync()
    {
        ++_filterVersion;
        _allCompetitors.Clear();
        return Task.CompletedTask;
    }
}
public partial class CompetitorDetailsPage : IResultsDataPage
{
    public async Task InvalidateDataAsync()
    {
        _lifetime.Activate();
        _detailsLoading = false;
        _vsLoadingFor = null;
        if (_competitor == null) return;
        var current = _competitor;
        var tab = VsView.IsVisible ? CompetitorDetailTab.Vs : CompetitionsView.IsVisible ? CompetitorDetailTab.Competitions
            : BadgesView.IsVisible ? CompetitorDetailTab.Badges : CompetitorDetailTab.Summary;
        var competitors = await CompetitorRepository.GetAllAsync();
        if (!ResultsDataRefreshService.IsVisible(this)) return;
        var updated = competitors.FirstOrDefault(f => string.Equals(f.FavoriteKey, current.FavoriteKey, StringComparison.OrdinalIgnoreCase))
            ?? new Competitor { FirstName = current.FirstName, LastName = current.LastName, Team = current.Team, IsFavorite = current.IsFavorite };
        SetCompetitor(updated);
        ShowTab(tab);
    }
}
public partial class CompetitionDetailsPage : IResultsDataPage
{
    public async Task InvalidateDataAsync()
    {
        _lifetime.Activate();
        _competitors = null;
        if (_competition == null) return;
        var old = _competition;
        var catalog = await CompetitionCatalogRepository.GetCatalogAsync();
        if (!ResultsDataRefreshService.IsVisible(this)) return;
        var updated = CompetitionCatalogService.FindCurrent(catalog, old)
            ?? new CompetitionCatalogEntry { Key = old.Key, CompetitionDate = old.CompetitionDate, CompetitionName = old.CompetitionName, CompetitionCity = old.CompetitionCity };
        var expanded = old.Divisions.Where(d => d.IsOtherParticipantsExpanded).Select(d => d.Division).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var division in updated.Divisions) division.IsOtherParticipantsExpanded = expanded.Contains(division.Division);
        SetCompetition(updated);
    }
}
public partial class CompetitionMedalLeaderboardPage : IResultsDataPage
{
    public async Task InvalidateDataAsync()
    {
        _lifetime.Activate();
        _loaded = false;
        _loading = false;
        if (_competition == null) return;
        var old = _competition;
        var catalog = await CompetitionCatalogRepository.GetCatalogAsync();
        if (!ResultsDataRefreshService.IsVisible(this)) return;
        _competition = CompetitionCatalogService.FindCurrent(catalog, old)
            ?? new CompetitionCatalogEntry { Key = old.Key, CompetitionDate = old.CompetitionDate, CompetitionName = old.CompetitionName, CompetitionCity = old.CompetitionCity };
        CompetitionLabel.Text = CompetitionUiHelpers.BuildCompetitionTitle(_competition.CompetitionDate, _competition.CompetitionName);
        CityLabel.Text = _competition.CompetitionCity;
    }
}
public sealed partial class DivisionMatchesPage : IResultsDataPage
{
    public Task InvalidateDataAsync()
    {
        _lifetime.Activate();
        _loaded = false;
        _loading = false;
        return Task.CompletedTask;
    }
    public Task RefreshDataAsync() => _loaded ? Task.CompletedTask : LoadAsync();
}
public partial class InformationPage : IResultsDataPage
{
    public Task InvalidateDataAsync()
    {
        _hasLoadedDataInformation = false;
        return Task.CompletedTask;
    }
}
