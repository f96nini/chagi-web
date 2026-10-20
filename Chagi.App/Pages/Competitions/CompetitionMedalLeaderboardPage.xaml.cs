using ITF_Taekwondo_Gradering.Logic;

namespace ITF_Taekwondo_Gradering;

public partial class CompetitionMedalLeaderboardPage : ContentPage, IQueryAttributable
{
    CompetitionCatalogEntry? _competition;
    ClubMedalLeaderboardNavigation? _clubScope;
    bool _loading;
    bool _loaded;
    bool _openingCompetitor;
    readonly PageLifetime _lifetime = new();

    public CompetitionMedalLeaderboardPage()
    {
        InitializeComponent();
        NavigationUiService.UseBackButton(this);
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("clubMedalLeaderboardKey", out var clubKey))
        {
            var scope = NavigationParameterStore.Take<ClubMedalLeaderboardNavigation>(clubKey?.ToString());
            if (scope == null) return;
            _clubScope = scope;
            SetCompetition(scope.Competition);
            ClubLabel.Text = scope.Club.Name;
            ClubLabel.IsVisible = true;
            EmptyStateLabel.Text = "Inga medaljer för valda filter.";
            Title = $"Medaljtoppen · {scope.Club.Name}";
        }
        if (query.TryGetValue("competitionKey", out var key))
        {
            var competition = NavigationParameterStore.Take<CompetitionCatalogEntry>(key?.ToString());
            if (competition == null)
                return;

            _clubScope = null;
            ClubLabel.IsVisible = false;
            EmptyStateLabel.Text = "Inga deltagare finns registrerade för tävlingen.";
            Title = "Medaljtoppen";
            SetCompetition(competition);
        }
    }

    void SetCompetition(CompetitionCatalogEntry competition)
    {
        _competition = competition;
        _loaded = false;
        CompetitionLabel.Text = CompetitionUiHelpers.BuildCompetitionTitle(competition.CompetitionDate, competition.CompetitionName);
        CityLabel.Text = competition.CompetitionCity;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _lifetime.Activate();
        await ResultsDataRefreshService.AppearingAsync(this);
    }

    public async Task RefreshDataAsync()
    {
        var token = _lifetime.Token;
        if (_competition == null || _loading || _loaded)
            return;

        _loading = true;
        try
        {
            await PageWorkService.RunAsync(this, async () =>
            {
                var competition = _competition;
                var scope = _clubScope;
                var classes = RankingClassFilterService.GetEnabledClasses();
                var groups = await Task.Run(() => CompetitionMedalLeaderboardService.BuildGroups(scope == null
                    ? competition.Divisions
                    : ClubCompetitionMedalsService.BuildDivisions(competition, scope.Club.Id, scope.Gender, scope.Discipline, classes)), token);
                token.ThrowIfCancellationRequested();
                LeaderboardView.ItemsSource = groups;
                EmptyStateLabel.IsVisible = groups.Count == 0;
                _loaded = true;
            }, "Sammanställer medaljtoppen...", token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex)
        {
            AppDiagnostics.Log(ex, "Ladda medaljtoppen");
            if (_lifetime.IsCurrent(token))
                await DisplayAlert("Fel", "Kunde inte sammanställa medaljtoppen. Öppna sidan igen för att försöka på nytt.", "OK");
        }
        finally
        {
            if (_lifetime.IsCurrent(token)) _loading = false;
        }
    }

    protected override void OnDisappearing()
    {
        ResultsDataRefreshService.Disappearing(this);
        _lifetime.Deactivate();
        _loading = false;
        _openingCompetitor = false;
        base.OnDisappearing();
    }

    async void OnParticipantTapped(object? sender, TappedEventArgs e)
    {
        if (_openingCompetitor || sender is not Border { BindingContext: CompetitionMedalLeaderboardEntry entry })
            return;

        _openingCompetitor = true;
        var token = _lifetime.Token;
        try
        {
            var competitor = await PageWorkService.RunBackgroundAsync(this, async () =>
            {
                var competitors = await CompetitorRepository.GetAllAsync().ConfigureAwait(false);
                return competitors.FirstOrDefault(competitor => competitor.Id == entry.CompetitorId)
                    ?? competitors.FirstOrDefault(competitor =>
                        string.Equals(competitor.FullName, entry.CompetitorName, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(competitor.Team, ClubCatalogService.NormalizeClubName(entry.Team), StringComparison.OrdinalIgnoreCase));
            }, "Laddar fighter...", token);
            token.ThrowIfCancellationRequested();

            if (competitor == null)
            {
                await DisplayAlert("Deltagare saknas", "Deltagaren kunde inte hittas. Öppna tävlingen igen för att uppdatera listan.", "OK");
                return;
            }

            await NavigationUiService.GoToWithParameterAsync(nameof(CompetitorDetailsPage), "competitorKey", competitor, this);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex)
        {
            AppDiagnostics.Log(ex, "Öppna deltagare från medaljtoppen");
            if (_lifetime.IsCurrent(token))
                await DisplayAlert("Fel", "Kunde inte öppna deltagarens detaljer. Försök igen.", "OK");
        }
        finally
        {
            if (_lifetime.IsCurrent(token)) _openingCompetitor = false;
        }
    }
}
