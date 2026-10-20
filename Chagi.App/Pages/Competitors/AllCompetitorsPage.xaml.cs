using ITF_Taekwondo_Gradering.Logic;
using ITF_Taekwondo_Gradering.Models.Ranking;

namespace ITF_Taekwondo_Gradering;

public partial class AllCompetitorsPage : ContentPage, IQueryAttributable
{
    readonly List<Competitor> _competitors = new();
    GenderFilterMode _selectedGender = ResultsFilterSettings.DefaultGender;
    CompetitionDisciplineE _selectedDiscipline;
    AllCompetitorSortMode _selectedSort = AllCompetitorSortMode.TotalPoints;
    readonly PageLifetime _lifetime = new();
    bool _isRefreshing;

    public AllCompetitorsPage()
    {
        InitializeComponent();
        NavigationUiService.UseBackButton(this);
        _selectedGender = GenderFilter.SelectedGender;
        _selectedDiscipline = ResultsFilterSettings.Discipline;
        InitializeOptions();
    }

    public AllCompetitorsPage(IEnumerable<Competitor> competitors, bool showMale, CompetitionDisciplineE discipline)
        : this()
    {
        _competitors.Clear();
        _competitors.AddRange(competitors);
        _selectedGender = GenderFilter.SelectedGender = showMale ? GenderFilterMode.Male : GenderFilterMode.Female;
        ResultsFilterSettings.Discipline = _selectedDiscipline = discipline;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("showMale", out var showMaleValue) &&
            bool.TryParse(showMaleValue?.ToString(), out var showMale))
        {
            _selectedGender = GenderFilter.SelectedGender = showMale ? GenderFilterMode.Male : GenderFilterMode.Female;
        }

        if (query.TryGetValue("discipline", out var disciplineValue) &&
            Enum.TryParse<CompetitionDisciplineE>(disciplineValue?.ToString(), out var discipline) && Enum.IsDefined(discipline))
        {
            ResultsFilterSettings.Discipline = _selectedDiscipline = discipline;
        }
    }

    void InitializeOptions()
    {
        SortPicker.ItemsSource = new List<string>
        {
            "Total Poäng",
            "Förnamn",
            "Efternamn",
            "Klubb"
        };
        SortPicker.SelectedIndex = 0;
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
        _selectedGender = GenderFilter.SelectedGender;
        _selectedDiscipline = ResultsFilterSettings.Discipline;

        SetBusyState(true, "Laddar fighters...", "Laddar fighters...");
        if (_competitors.Count == 0)
        {
            try
            {
                _competitors.AddRange(await CompetitorRepository.GetAllAsync().WaitAsync(token));
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { return; }
            catch (Exception ex)
            {
                AppDiagnostics.Log(ex, "Ladda alla fighters");
                if (_lifetime.IsCurrent(token)) SetBusyState(false, "Kunde inte läsa fighters.");
                return;
            }
        }

        await RefreshSectionsAsync();
    }

    protected override void OnDisappearing()
    {
        ResultsDataRefreshService.Disappearing(this);
        _lifetime.Deactivate();
        _isRefreshing = false;
        SetBusyState(false);
        base.OnDisappearing();
    }

    async void OnClassesClicked(object? sender, EventArgs e)
    {
        await NavigationUiService.GoToAsync(
            nameof(RankingClassesPage));
    }

    async void OnGenderChanged(object? sender, EventArgs e)
    {
        _selectedGender = GenderFilter.SelectedGender;
        RefreshStyles();
        await RefreshSectionsAsync();
    }

    async void OnAllClicked(object? sender, EventArgs e)
    {
        ResultsFilterSettings.Discipline = _selectedDiscipline = CompetitionDisciplineE.Alla;
        await RefreshSectionsAsync();
    }

    async void OnTulClicked(object? sender, EventArgs e)
    {
        ResultsFilterSettings.Discipline = _selectedDiscipline = CompetitionDisciplineE.Tul;
        await RefreshSectionsAsync();
    }

    async void OnSparringClicked(object? sender, EventArgs e)
    {
        ResultsFilterSettings.Discipline = _selectedDiscipline = CompetitionDisciplineE.Sparring;
        await RefreshSectionsAsync();
    }

    async void OnSpecClicked(object? sender, EventArgs e)
    {
        ResultsFilterSettings.Discipline = _selectedDiscipline = CompetitionDisciplineE.Specialtekniker;
        await RefreshSectionsAsync();
    }

    async void OnKrossClicked(object? sender, EventArgs e)
    {
        ResultsFilterSettings.Discipline = _selectedDiscipline = CompetitionDisciplineE.Kross;
        await RefreshSectionsAsync();
    }

    async void OnSortPickerChanged(object? sender, EventArgs e)
    {
        _selectedSort = SortPicker.SelectedIndex switch
        {
            1 => AllCompetitorSortMode.FirstName,
            2 => AllCompetitorSortMode.LastName,
            3 => AllCompetitorSortMode.Team,
            _ => AllCompetitorSortMode.TotalPoints
        };

        await RefreshSectionsAsync();
    }

    async void OnFavoriteClicked(object? sender, EventArgs e)
    {
        if (sender is not BindableObject bindable || bindable.BindingContext is not RankedCompetitorEntry entry)
            return;

        await CompetitorRepository.SetFavoriteAsync(entry.Competitor, !entry.Competitor.IsFavorite);
        await RefreshSectionsAsync();
    }

    async void OnEntryTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not BindableObject bindable || bindable.BindingContext is not RankedCompetitorEntry entry)
            return;

        await NavigationUiService.GoToWithParameterAsync(nameof(CompetitorDetailsPage), "competitorKey", entry.Competitor, this);
    }

    async Task RefreshSectionsAsync()
    {
        var token = _lifetime.Token;
        if (_isRefreshing || token.IsCancellationRequested) return;
        _isRefreshing = true;
        SetBusyState(true, "Laddar alla competitors...", "Bygger listan...");
        try
        {
            RefreshStyles();
            await Task.Yield();

            var competitors = _competitors.ToArray();
            var sections = await Task.Run(() => BuildSections(competitors), token).WaitAsync(token);
            token.ThrowIfCancellationRequested();

            ResultsView.ItemsSource = sections
                .Select(s => new RankingSectionGroup(s.Title, s.Entries))
                .ToList();

            StatusLabel.Text = RankingClassFilterService.GetEnabledClasses().Count == 0
                ? "Inga klasser valda. Välj klasser via Klasser."
                : $"{sections.Sum(s => s.Count)} fighters";
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex)
        {
            AppDiagnostics.Log(ex, "Uppdatera alla fighters");
            if (_lifetime.IsCurrent(token)) StatusLabel.Text = "Kunde inte uppdatera listan.";
        }
        finally
        {
            if (_lifetime.IsCurrent(token))
            {
                _isRefreshing = false;
                SetBusyState(false);
            }
        }
    }

    void SetBusyState(bool isBusy, string? statusText = null, string? overlayText = null)
    {
        BusyIndicator.IsVisible = isBusy;
        BusyIndicator.IsRunning = isBusy;
        LoadingOverlay.IsVisible = isBusy;
        OverlayIndicator.IsVisible = isBusy;
        OverlayIndicator.IsRunning = isBusy;

        if (statusText != null)
            StatusLabel.Text = statusText;

        if (overlayText != null)
            OverlayLabel.Text = overlayText;

        SetControlState(!isBusy);
    }

    void SetControlState(bool isEnabled)
    {
        GenderFilter.IsEnabled = isEnabled;
        AllButton.IsEnabled = isEnabled;
        TulButton.IsEnabled = isEnabled;
        SparringButton.IsEnabled = isEnabled;
        SpecButton.IsEnabled = isEnabled;
        KrossButton.IsEnabled = isEnabled;
        SortPicker.IsEnabled = isEnabled;
    }

    void RefreshStyles()
    {
        SetTabButtonState(AllButton, _selectedDiscipline == CompetitionDisciplineE.Alla);
        SetTabButtonState(TulButton, _selectedDiscipline == CompetitionDisciplineE.Tul);
        SetTabButtonState(SparringButton, _selectedDiscipline == CompetitionDisciplineE.Sparring);
        SetTabButtonState(SpecButton, _selectedDiscipline == CompetitionDisciplineE.Specialtekniker);
        SetTabButtonState(KrossButton, _selectedDiscipline == CompetitionDisciplineE.Kross);
    }

    List<AllCompetitorSection> BuildSections(IEnumerable<Competitor> competitors)
    {
        var enabledClasses = RankingClassFilterService.GetEnabledClasses().ToHashSet();
        var entries = competitors
            .Where(MatchesSelectedGender)
            .Where(c => enabledClasses.Count > 0 && (!c.CurrentAgeClass.HasValue || enabledClasses.Contains(c.CurrentAgeClass.Value)))
            .Select(BuildEntry)
            .ToList();

        var rankedEntries = RankingPositionService
            .OrderByScoreAndMedals(
                entries,
                e => e.Score,
                e => e.GoldCount,
                e => e.SilverCount,
                e => e.BronzeCount)
            .ThenBy(e => e.FullName, SwedishNameComparer.Instance)
            .ThenBy(e => e.Team, SwedishNameComparer.Instance)
            .ToList();

        ApplyPositions(rankedEntries);

        return _selectedSort switch
        {
            AllCompetitorSortMode.Team => BuildTeamSections(rankedEntries),
            AllCompetitorSortMode.FirstName => new List<AllCompetitorSection> { BuildFirstNameSection(rankedEntries) },
            AllCompetitorSortMode.LastName => new List<AllCompetitorSection> { BuildLastNameSection(rankedEntries) },
            _ => new List<AllCompetitorSection> { BuildTotalPointsSection(rankedEntries) }
        };
    }

    bool MatchesSelectedGender(Competitor competitor)
    {
        return _selectedGender switch
        {
            GenderFilterMode.Male => competitor.IsMale == true,
            GenderFilterMode.Female => competitor.IsMale == false,
            _ => true
        };
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

    RankedCompetitorEntry BuildEntry(Competitor competitor)
    {
        var relevantResults = competitor.Results
            .Where(r => _selectedDiscipline == CompetitionDisciplineE.Alla || r.Discipline == _selectedDiscipline)
            .ToList();

        return new RankedCompetitorEntry
        {
            Competitor = competitor,
            FullName = competitor.FullName,
            Team = string.IsNullOrWhiteSpace(competitor.Team) ? "Ingen klubb" : competitor.Team.Trim(),
            Score = relevantResults.Sum(r => r.PointsAwarded),
            Rating = relevantResults.Sum(r => r.RatingAwarded),
            ResultCount = relevantResults.Count,
            GoldCount = relevantResults.Count(r => r.Placement == 1),
            SilverCount = relevantResults.Count(r => r.Placement == 2),
            BronzeCount = relevantResults.Count(r => r.Placement == 3),
            IsFavorite = competitor.IsFavorite,
            HasResults = relevantResults.Count > 0,
        };
    }

    static AllCompetitorSection BuildTotalPointsSection(List<RankedCompetitorEntry> entries)
    {
        return new AllCompetitorSection("Total Poäng", entries.ToList());
    }

    static AllCompetitorSection BuildFirstNameSection(List<RankedCompetitorEntry> entries)
    {
        var orderedEntries = entries
            .OrderBy(e => e.Competitor.FirstName, SwedishNameComparer.Instance)
            .ThenBy(e => e.Competitor.LastName, SwedishNameComparer.Instance)
            .ThenBy(e => e.Position)
            .ThenBy(e => e.Team, SwedishNameComparer.Instance)
            .ToList();

        return new AllCompetitorSection("Förnamn", orderedEntries);
    }

    static AllCompetitorSection BuildLastNameSection(List<RankedCompetitorEntry> entries)
    {
        var orderedEntries = entries
            .OrderBy(e => e.Competitor.LastName, SwedishNameComparer.Instance)
            .ThenBy(e => e.Competitor.FirstName, SwedishNameComparer.Instance)
            .ThenBy(e => e.Position)
            .ThenBy(e => e.Team, SwedishNameComparer.Instance)
            .ToList();

        return new AllCompetitorSection("Efternamn", orderedEntries);
    }

    static List<AllCompetitorSection> BuildTeamSections(List<RankedCompetitorEntry> entries)
    {
        return entries
            .GroupBy(e => e.Team)
            .OrderBy(g => g.Key, SwedishNameComparer.Instance)
            .Select(group =>
            {
                var orderedEntries = group
                    .OrderBy(e => e.Position)
                    .ThenBy(e => e.FullName, SwedishNameComparer.Instance)
                    .ToList();

                return new AllCompetitorSection(group.Key, orderedEntries);
            })
            .ToList();
    }

    static void ApplyPositions(List<RankedCompetitorEntry> entries)
    {
        RankingPositionService.ApplySharedPositions(
            entries,
            (left, right) => RankingPositionService.HaveSameScoreAndMedals(
                left,
                right,
                e => e.Score,
                e => e.GoldCount,
                e => e.SilverCount,
                e => e.BronzeCount),
            (entry, position) => entry.Position = position,
            (entry, label) => entry.PositionLabel = label);
    }
}

public sealed class RankingSectionGroup : List<RankedCompetitorEntry>
{
    public RankingSectionGroup(string title, IEnumerable<RankedCompetitorEntry> entries)
        : base(entries)
    {
        Title = title;
    }

    public string Title { get; }
}

enum AllCompetitorSortMode
{
    TotalPoints = 1,
    FirstName = 2,
    LastName = 3,
    Team = 4,
}

sealed class AllCompetitorSection
{
    public AllCompetitorSection(string title, List<RankedCompetitorEntry> entries)
    {
        Title = title;
        Entries = entries;
    }

    public string Title { get; }
    public List<RankedCompetitorEntry> Entries { get; }
    public int Count => Entries.Count;
}
