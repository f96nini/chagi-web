using ITF_Taekwondo_Gradering.Logic;
using ITF_Taekwondo_Gradering.Models.Ranking;
using ITF_Taekwondo_Gradering.Views;

namespace ITF_Taekwondo_Gradering;

public partial class CompetitionDetailsPage : ContentPage, IQueryAttributable
{
    CompetitionCatalogEntry? _competition;
    readonly Command<CompetitionPlacementEntry> _openCompetitorCommand;
    public Command<CompetitionDivisionEntry> OpenDivisionCommand { get; }
    bool _openingDivision;
    bool _openingMedalLeaderboard;
    GenderFilterMode _selectedGender = ResultsFilterSettings.DefaultGender;
    CompetitionDisciplineE? _selectedDiscipline;
    List<Competitor>? _competitors;
    List<CompetitionDivisionEntry>? _renderedDivisions;
    readonly PageLifetime _lifetime = new();
    int _renderRequest;
    int _renderedClassVersion = -1;
    int _matchDataVersion = -1;
    CompetitionCatalogEntry? _matchesLoadedFor;
    bool _openingCompetitor;

    public CompetitionDetailsPage()
    {
        InitializeComponent();
        _openCompetitorCommand = new Command<CompetitionPlacementEntry>(async result => await OpenCompetitorDetailsAsync(result));
        OpenDivisionCommand = new Command<CompetitionDivisionEntry>(async division => await OpenDivisionAsync(division));
        NavigationUiService.UseBackButton(this);
        ToolbarItems.Add(new ToolbarItem("Klasser", null, async () =>
            await NavigationUiService.GoToAsync(
                nameof(RankingClassesPage))));
        _selectedGender = GenderFilter.SelectedGender;
        _selectedDiscipline = ResultsFilterSettings.Discipline == CompetitionDisciplineE.Alla
            ? null : ResultsFilterSettings.Discipline;

        RefreshFilterStyles();
    }

    public CompetitionDetailsPage(CompetitionCatalogEntry competition)
        : this()
    {
        SetCompetition(competition);
    }

    public Command<CompetitionPlacementEntry> OpenCompetitorCommand => _openCompetitorCommand;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("competitionKey", out var value))
        {
            var competition = NavigationParameterStore.Take<CompetitionCatalogEntry>(value?.ToString());
            if (competition != null)
                SetCompetition(competition);
        }
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
        try
        {
            var gender = GenderFilter.SelectedGender;
            var savedDiscipline = ResultsFilterSettings.Discipline;
            CompetitionDisciplineE? discipline = savedDiscipline != CompetitionDisciplineE.Alla && HasDiscipline(savedDiscipline)
                ? savedDiscipline : null;
            var filtersChanged = _selectedGender != gender || _selectedDiscipline != discipline;
            _selectedGender = gender;
            _selectedDiscipline = discipline;
            RefreshFilterStyles();
            if (filtersChanged || _renderedDivisions == null || _renderedClassVersion != RankingClassFilterService.GetChangeVersion())
                await RenderDivisionsAsync();
            token.ThrowIfCancellationRequested();
            if (!ReferenceEquals(_matchesLoadedFor, _competition) || _matchDataVersion != CompetitorRepository.GetChangeVersion())
                await PageWorkService.RunAsync(this, () => RefreshMatchAvailabilityAsync(token), "Laddar matchinformation...", token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex) { AppDiagnostics.Log(ex, "Visa tävlingsresultat"); }
    }

    protected override void OnDisappearing()
    {
        ResultsDataRefreshService.Disappearing(this);
        _lifetime.Deactivate();
        _openingCompetitor = false;
        base.OnDisappearing();
    }

    async Task RefreshMatchAvailabilityAsync(CancellationToken token)
    {
        var competition = _competition;
        if (competition == null) return;
        var dataVersion = CompetitorRepository.GetChangeVersion();
        try
        {
            var available = await CompetitionMatchService.GetAvailableDivisionsAsync(
                competition.CompetitionDate, competition.CompetitionName, token);
            if (!_lifetime.IsCurrent(token) || !ReferenceEquals(competition, _competition)) return;
            foreach (var division in competition.Divisions)
                division.HasMatchData = available.Contains(division.Division.Trim());
            _matchesLoadedFor = competition;
            _matchDataVersion = dataVersion;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex)
        {
            AppDiagnostics.Log(ex, "Läs matchtillgänglighet");
            if (_lifetime.IsCurrent(token))
                foreach (var division in competition.Divisions) division.HasMatchData = false;
        }
    }

    async Task EnsureCompetitorsLoadedAsync(CancellationToken token)
    {
        if (_competitors != null)
            return;

        _competitors = await PageWorkService.RunBackgroundAsync(this, CompetitorRepository.GetAllAsync,
            cancellationToken: token);
    }

    async void OnGenderChanged(object? sender, EventArgs e)
    {
        _selectedGender = GenderFilter.SelectedGender;
        RefreshFilterStyles();
        await RenderDivisionsAsync();
    }

    async void OnAllClicked(object? sender, EventArgs e)
    {
        _selectedDiscipline = null;
        ResultsFilterSettings.Discipline = CompetitionDisciplineE.Alla;
        RefreshFilterStyles();
        await RenderDivisionsAsync();
    }

    async void OnTulClicked(object? sender, EventArgs e)
    {
        _selectedDiscipline = ResultsFilterSettings.Discipline = CompetitionDisciplineE.Tul;
        RefreshFilterStyles();
        await RenderDivisionsAsync();
    }

    async void OnSparringClicked(object? sender, EventArgs e)
    {
        _selectedDiscipline = ResultsFilterSettings.Discipline = CompetitionDisciplineE.Sparring;
        RefreshFilterStyles();
        await RenderDivisionsAsync();
    }

    async void OnSpecClicked(object? sender, EventArgs e)
    {
        _selectedDiscipline = ResultsFilterSettings.Discipline = CompetitionDisciplineE.Specialtekniker;
        RefreshFilterStyles();
        await RenderDivisionsAsync();
    }

    async void OnKrossClicked(object? sender, EventArgs e)
    {
        _selectedDiscipline = ResultsFilterSettings.Discipline = CompetitionDisciplineE.Kross;
        RefreshFilterStyles();
        await RenderDivisionsAsync();
    }

    async Task RenderDivisionsAsync()
    {
        var token = _lifetime.Token;
        if (token.IsCancellationRequested) return;
        var request = ++_renderRequest;
        if (_competition == null)
        {
            EmptyStateLabel.IsVisible = false;
            ClubCompetitionCardView.Entries = Array.Empty<Models.Clubs.CompetitionClubStandingEntry>();
            ClubCompetitionCardView.IsVisible = false;
            DivisionsView.ItemsSource = null;
            _renderedDivisions = null;
            return;
        }

        var competition = _competition;
        var gender = _selectedGender;
        var discipline = _selectedDiscipline;
        var enabledClasses = RankingClassFilterService.GetEnabledClasses().ToHashSet();
        var classVersion = RankingClassFilterService.GetChangeVersion();
        try
        {
            await PageWorkService.RunAsync(this, async () =>
            {
                var data = await Task.Run(() =>
                {
                    var divisions = competition.Divisions
                        .Where(d => gender == GenderFilterMode.All ||
                            (gender == GenderFilterMode.Male ? d.IsMale != false : d.IsMale != true))
                        .Where(d => enabledClasses.Count > 0 && (!d.AgeClass.HasValue || enabledClasses.Contains(d.AgeClass.Value)))
                        .Where(d => !discipline.HasValue || d.Discipline == discipline.Value)
                        .ToList();
                    return (Divisions: divisions, Standings: CompetitionClubSummaryService.BuildStandings(divisions));
                }, token);
                if (!_lifetime.IsCurrent(token) || request != _renderRequest || classVersion != RankingClassFilterService.GetChangeVersion()
                    || !ReferenceEquals(competition, _competition) || gender != _selectedGender || discipline != _selectedDiscipline)
                    return;
                _renderedClassVersion = classVersion;
                EmptyStateLabel.Text = enabledClasses.Count == 0
                    ? "Inga klasser valda. Välj klasser via Klasser för att visa resultat."
                    : "Inga resultat för valt filter.";
                if (_renderedDivisions != null && _renderedDivisions.SequenceEqual(data.Divisions))
                    return;
                ClubCompetitionCardView.Entries = data.Standings;
                ClubCompetitionCardView.IsVisible = data.Standings.Count > 0;
                EmptyStateLabel.IsVisible = data.Divisions.Count == 0;
                DivisionsView.ItemsSource = data.Divisions;
                _renderedDivisions = data.Divisions;
            }, "Sammanställer tävlingen...", token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex)
        {
            AppDiagnostics.Log(ex, "Sammanställ tävlingen");
            if (_lifetime.IsCurrent(token))
                await DisplayAlert("Fel", "Kunde inte sammanställa tävlingen.", "OK");
        }
    }

    void RefreshFilterStyles()
    {
        UpdateDisciplineFilterVisibility();
        SetTabButtonState(AllButton, !_selectedDiscipline.HasValue);
        SetTabButtonState(TulButton, _selectedDiscipline == CompetitionDisciplineE.Tul);
        SetTabButtonState(SparringButton, _selectedDiscipline == CompetitionDisciplineE.Sparring);
        SetTabButtonState(SpecButton, _selectedDiscipline == CompetitionDisciplineE.Specialtekniker);
        SetTabButtonState(KrossButton, _selectedDiscipline == CompetitionDisciplineE.Kross);
    }

    void UpdateDisciplineFilterVisibility()
    {
        if (_competition == null)
        {
            TulButton.IsVisible = false;
            SparringButton.IsVisible = false;
            SpecButton.IsVisible = false;
            KrossButton.IsVisible = false;
            _selectedDiscipline = null;
            return;
        }

        TulButton.IsVisible = HasDiscipline(CompetitionDisciplineE.Tul);
        SparringButton.IsVisible = HasDiscipline(CompetitionDisciplineE.Sparring);
        SpecButton.IsVisible = HasDiscipline(CompetitionDisciplineE.Specialtekniker);
        KrossButton.IsVisible = HasDiscipline(CompetitionDisciplineE.Kross);

        if (_selectedDiscipline.HasValue && !HasDiscipline(_selectedDiscipline.Value))
        {
            _selectedDiscipline = null;
        }
    }

    bool HasDiscipline(CompetitionDisciplineE discipline)
    {
        return _competition?.Divisions.Any(division => division.Discipline == discipline) == true;
    }

    static void SetTabButtonState(Button button, bool isSelected)
    {
        if (isSelected)
        {
            button.BackgroundColor = Color.FromArgb("#184E77");
            button.TextColor = Colors.White;
        }
        else
        {
            button.SetAppThemeColor(Button.BackgroundColorProperty, ThemeColors.CardStrokeLight, ThemeColors.SurfaceDark);
            button.SetAppThemeColor(Button.TextColorProperty, ThemeColors.PrimaryTextLight, ThemeColors.PrimaryTextDark);
        }
    }

    async Task OpenCompetitorDetailsAsync(CompetitionPlacementEntry result)
    {
        if (_openingCompetitor) return;
        var token = _lifetime.Token;
        if (token.IsCancellationRequested) return;
        _openingCompetitor = true;
        try
        {
            await EnsureCompetitorsLoadedAsync(token);
            token.ThrowIfCancellationRequested();

            var competitor = _competitors?.FirstOrDefault(c => c.Id == result.CompetitorId);
            if (competitor == null)
                return;

            await NavigationUiService.GoToWithParameterAsync(nameof(CompetitorDetailsPage), "competitorKey", competitor, this);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex)
        {
            AppDiagnostics.Log(ex, "Öppna deltagare från tävling");
            if (_lifetime.IsCurrent(token)) await DisplayAlert("Fel", "Kunde inte öppna deltagaren. Försök igen.", "OK");
        }
        finally { if (_lifetime.IsCurrent(token)) _openingCompetitor = false; }
    }

    async Task OpenDivisionAsync(CompetitionDivisionEntry division)
    {
        if (_competition == null || _openingDivision || !division.HasMatchData)
            return;
        _openingDivision = true;
        try
        {
            await NavigationUiService.GoToWithParameterAsync(nameof(DivisionMatchesPage), "divisionKey", new DivisionMatchNavigation(_competition, division), this);
        }
        finally
        {
            _openingDivision = false;
        }
    }

    async void OnMedalLeaderboardTapped(object? sender, TappedEventArgs e)
    {
        if (_competition == null || _openingMedalLeaderboard)
            return;

        _openingMedalLeaderboard = true;
        try
        {
            await NavigationUiService.GoToWithParameterAsync(nameof(CompetitionMedalLeaderboardPage), "competitionKey", _competition, this);
        }
        finally
        {
            _openingMedalLeaderboard = false;
        }
    }

    void SetCompetition(CompetitionCatalogEntry competition)
    {
        _competition = competition;
        _renderedDivisions = null;
        _renderedClassVersion = -1;
        _matchesLoadedFor = null;
        MedalLeaderboardButton.IsVisible = true;
        Title = CompetitionUiHelpers.BuildCompetitionTitle(competition.CompetitionDate, competition.CompetitionName);
        TitleLabel.Text = Title;
        CityLabel.Text = competition.CompetitionCity;
        UpdateDisciplineFilterVisibility();
        RefreshFilterStyles();
    }
}
