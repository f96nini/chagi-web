using ITF_Taekwondo_Gradering.Logic;
using ITF_Taekwondo_Gradering.Models.Clubs;
using ITF_Taekwondo_Gradering.Models.Ranking;
using ITF_Taekwondo_Gradering.Views;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Layouts;

namespace ITF_Taekwondo_Gradering;

public sealed record ClubCompetitionNavigation(ClubSummaryEntry Club, CompetitionCatalogEntry Competition);
public sealed record ClubMedalLeaderboardNavigation(ClubSummaryEntry Club, CompetitionCatalogEntry Competition,
    GenderFilterMode Gender, CompetitionDisciplineE Discipline);

public sealed class ClubCompetitionDetailsPage : ContentPage, IQueryAttributable, IResultsDataPage
{
    readonly PageLifetime _lifetime = new();
    readonly Label _clubName = new() { FontSize = 22, FontAttributes = FontAttributes.Bold, TextColor = Colors.White };
    readonly Image _logo = new() { WidthRequest = 48, HeightRequest = 48, Aspect = Aspect.AspectFit };
    readonly Label _city = new() { FontSize = 14, TextColor = Colors.White };
    readonly Label _summary = new() { FontSize = 14, TextColor = Colors.White, IsVisible = false };
    readonly Label _status = new() { FontSize = 14, TextColor = Colors.White };
    readonly ActivityIndicator _busy = new() { Color = Colors.White, IsVisible = false, WidthRequest = 24, HeightRequest = 24 };
    readonly Button _fullCompetitionButton = new() { LineBreakMode = LineBreakMode.WordWrap, IsEnabled = false };
    readonly MedalLeaderboardLink _medalLeaderboardButton = new();
    readonly CollectionView _divisions = new() { SelectionMode = SelectionMode.None, BackgroundColor = Colors.Transparent };
    readonly GenderFilterView _genderFilter = new();
    readonly Dictionary<CompetitionDisciplineE, Button> _disciplineButtons = new();
    ClubCompetitionNavigation? _context;
    CompetitionCatalogEntry? _competition;
    int _request;
    bool _openingCompetition;
    bool _openingCompetitor;
    bool _openingLeaderboard;

    public ClubCompetitionDetailsPage()
    {
        Title = "Klubbens medaljer";
        NavigationUiService.UseBackButton(this);
        ToolbarItems.Add(new ToolbarItem("Klasser", null, async () =>
            await NavigationUiService.GoToAsync(nameof(RankingClassesPage), this)));

        var heading = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star) },
            ColumnSpacing = 12
        };
        heading.Add(new Border { Content = _logo, BackgroundColor = Colors.White, Padding = 6,
            StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 14 } });
        _clubName.VerticalOptions = LayoutOptions.Center;
        heading.Add(_clubName, 1, 0);

        _genderFilter.SelectionChanged += async (_, _) => await RefreshDataAsync();

        var disciplines = new FlexLayout
        {
            Direction = FlexDirection.Row, Wrap = FlexWrap.Wrap, AlignItems = FlexAlignItems.Start,
            AlignContent = FlexAlignContent.Start, JustifyContent = FlexJustify.SpaceBetween
        };
        foreach (var (discipline, text) in new[]
        {
            (CompetitionDisciplineE.Alla, "Alla"), (CompetitionDisciplineE.Tul, "Tul"),
            (CompetitionDisciplineE.Sparring, "Sparring"), (CompetitionDisciplineE.Specialtekniker, "Spec"),
            (CompetitionDisciplineE.Kross, "Kross")
        })
        {
            var button = new Button { Text = text, Padding = new Thickness(10), CornerRadius = 12, Margin = new Thickness(0, 0, 6, 6) };
            FlexLayout.SetShrink(button, 0);
            button.Clicked += async (_, _) => { ResultsFilterSettings.Discipline = discipline; await RefreshDataAsync(); };
            _disciplineButtons.Add(discipline, button);
            disciplines.Add(button);
        }

        QuickButtonStyle.Apply(_fullCompetitionButton);
        _fullCompetitionButton.FontSize = 18;
        SemanticProperties.SetHint(_fullCompetitionButton, "Visa tävlingens samtliga resultat");
        _fullCompetitionButton.Clicked += async (_, _) => await OpenFullCompetitionAsync();
        _medalLeaderboardButton.Tapped += async (_, _) => await OpenMedalLeaderboardAsync();
        var openCompetitor = new Command<CompetitionPlacementEntry>(async result => await OpenCompetitorAsync(result));
        _divisions.ItemsLayout = new LinearItemsLayout(ItemsLayoutOrientation.Vertical) { ItemSpacing = 12 };
        _divisions.ItemTemplate = new DataTemplate(() =>
        {
            var card = new CompetitionDetailsCard { CompetitorSelectedCommand = openCompetitor, Margin = new Thickness(0, 0, 0, 2) };
            card.SetBinding(CompetitionDetailsCard.DivisionProperty, ".");
            return card;
        });
        var statusRow = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star) }, ColumnSpacing = 8
        };
        statusRow.Add(_busy);
        statusRow.Add(_status, 1, 0);
        _divisions.Header = new VerticalStackLayout
        {
            Spacing = 12, Margin = new Thickness(0, 0, 0, 12),
            Children = { heading, _fullCompetitionButton, _city, _genderFilter, disciplines,
                _summary, _medalLeaderboardButton, statusRow }
        };
        Content = new AppBackgroundView { Children = { new Grid { Padding = 12, Children = { _divisions } } } };
        RefreshStyles();
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (!query.TryGetValue("clubCompetitionKey", out var key)) return;
        var context = NavigationParameterStore.Take<ClubCompetitionNavigation>(key?.ToString());
        if (context == null) return;
        _context = context;
        _clubName.Text = context.Club.Name;
        _logo.Source = context.Club.ImageSource;
        SemanticProperties.SetDescription(_logo, $"Klubbild för {context.Club.Name}");
        SetCompetition(context.Competition);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _lifetime.Activate();
        await ResultsDataRefreshService.AppearingAsync(this);
    }

    protected override void OnDisappearing()
    {
        ResultsDataRefreshService.Disappearing(this);
        _lifetime.Deactivate();
        ++_request;
        _openingCompetition = _openingCompetitor = false;
        _openingLeaderboard = false;
        base.OnDisappearing();
    }

    public Task InvalidateDataAsync() => Task.CompletedTask;

    public async Task RefreshDataAsync()
    {
        var context = _context;
        var token = _lifetime.Token;
        if (context == null || token.IsCancellationRequested) return;
        var request = ++_request;
        var gender = _genderFilter.SelectedGender;
        var discipline = ResultsFilterSettings.Discipline;
        var classes = RankingClassFilterService.GetEnabledClasses();
        RefreshStyles();
        _busy.IsVisible = _busy.IsRunning = true;
        _status.IsVisible = true;
        _status.Text = "Sammanställer klubbens medaljer...";
        try
        {
            var catalog = await CompetitionCatalogRepository.GetCatalogAsync().WaitAsync(token);
            var competition = CompetitionCatalogService.FindCurrent(catalog, context.Competition)
                ?? new CompetitionCatalogEntry { CompetitionDate = context.Competition.CompetitionDate,
                    CompetitionName = context.Competition.CompetitionName, CompetitionCity = context.Competition.CompetitionCity };
            var divisions = await Task.Run(() => ClubCompetitionMedalsService.BuildDivisions(competition,
                context.Club.Id, gender, discipline, classes), token);
            if (!_lifetime.IsCurrent(token) || request != _request) return;
            SetCompetition(competition);
            _divisions.ItemsSource = divisions;
            var medals = divisions.SelectMany(division => division.Results).ToList();
            var gold = medals.Count(result => result.Placement == 1);
            var silver = medals.Count(result => result.Placement == 2);
            var bronze = medals.Count(result => result.Placement == 3);
            _summary.Text = $"{gold} guld · {silver} silver · {bronze} brons · {gold * 5 + silver * 3 + bronze} medaljpoäng";
            _summary.IsVisible = true;
            _status.Text = classes.Count == 0 ? "Inga klasser valda. Välj klasser via Klasser."
                : "Inga medaljer för valda filter.";
            _status.IsVisible = medals.Count == 0;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex)
        {
            AppDiagnostics.Log(ex, "Ladda klubbens tävlingsmedaljer");
            if (_lifetime.IsCurrent(token) && request == _request) _status.Text = "Kunde inte läsa klubbens medaljer.";
        }
        finally
        {
            if (_lifetime.IsCurrent(token) && request == _request) _busy.IsVisible = _busy.IsRunning = false;
        }
    }

    void SetCompetition(CompetitionCatalogEntry competition)
    {
        // Retain the complete catalog entry for the explicit link to all clubs' results.
        _competition = competition;
        _fullCompetitionButton.Text = CompetitionUiHelpers.BuildCompetitionTitle(competition.CompetitionDate, competition.CompetitionName);
        _city.Text = competition.CompetitionCity;
        _city.IsVisible = !string.IsNullOrWhiteSpace(competition.CompetitionCity);
        _fullCompetitionButton.IsEnabled = true;
    }

    void RefreshStyles()
    {
        foreach (var (discipline, button) in _disciplineButtons) StyleFilter(button, discipline == ResultsFilterSettings.Discipline);
    }

    static void StyleFilter(Button button, bool selected)
    {
        var background = Color.FromArgb("#184E77");
        button.SetAppThemeColor(Button.BackgroundColorProperty,
            selected ? background : ThemeColors.CardStrokeLight,
            selected ? background : ThemeColors.SurfaceDark);
        button.SetAppThemeColor(Button.TextColorProperty,
            selected ? Colors.White : ThemeColors.PrimaryTextLight, selected ? Colors.White : ThemeColors.PrimaryTextDark);
    }

    async Task OpenFullCompetitionAsync()
    {
        if (_competition == null || _openingCompetition) return;
        _openingCompetition = true;
        try
        {
            await NavigationUiService.GoToWithParameterAsync(nameof(CompetitionDetailsPage), "competitionKey", _competition, this);
        }
        finally { _openingCompetition = false; }
    }

    async Task OpenMedalLeaderboardAsync()
    {
        if (_competition == null || _context == null || _openingLeaderboard) return;
        _openingLeaderboard = true;
        try
        {
            await NavigationUiService.GoToWithParameterAsync(nameof(CompetitionMedalLeaderboardPage), "clubMedalLeaderboardKey",
                new ClubMedalLeaderboardNavigation(_context.Club, _competition, _genderFilter.SelectedGender,
                    ResultsFilterSettings.Discipline), this);
        }
        finally { _openingLeaderboard = false; }
    }

    async Task OpenCompetitorAsync(CompetitionPlacementEntry result)
    {
        var token = _lifetime.Token;
        if (_openingCompetitor || token.IsCancellationRequested) return;
        _openingCompetitor = true;
        try
        {
            var competitors = await CompetitorRepository.GetAllAsync().WaitAsync(token);
            if (!_lifetime.IsCurrent(token)) return;
            var competitor = competitors.FirstOrDefault(fighter => fighter.Id == result.CompetitorId)
                ?? competitors.FirstOrDefault(fighter => string.Equals(fighter.FullName, result.CompetitorName, StringComparison.OrdinalIgnoreCase)
                    && ClubCatalogService.ResolveClub(fighter.Team)?.Id == _context?.Club.Id);
            if (competitor != null)
                await NavigationUiService.GoToWithParameterAsync(nameof(CompetitorDetailsPage), "competitorKey", competitor, this);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex) { AppDiagnostics.Log(ex, "Öppna klubbens medaljör"); }
        finally { if (_lifetime.IsCurrent(token)) _openingCompetitor = false; }
    }
}
