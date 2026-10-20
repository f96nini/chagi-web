using ITF_Taekwondo_Gradering.Logic;
using System.Globalization;
using ITF_Taekwondo_Gradering.Models.Clubs;
using ITF_Taekwondo_Gradering.Models.Ranking;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Layouts;

namespace ITF_Taekwondo_Gradering;

public sealed class ClubDetailsPage : ContentPage, IQueryAttributable, IResultsDataPage
{
    readonly PageLifetime _lifetime = new();
    readonly Label _name = new() { FontSize = 22, FontAttributes = FontAttributes.Bold, TextColor = Colors.White };
    readonly Image _logo = new() { WidthRequest = 64, HeightRequest = 64, Aspect = Aspect.AspectFit };
    readonly Label _status = new() { TextColor = Colors.White, FontSize = 13 };
    readonly ActivityIndicator _busy = new() { Color = Colors.White, IsVisible = false, WidthRequest = 24, HeightRequest = 24 };
    readonly CollectionView _results = new() { SelectionMode = SelectionMode.Single, BackgroundColor = Colors.Transparent };
    readonly GenderFilterView _genderFilter = new();
    readonly Dictionary<CompetitionDisciplineE, Button> _disciplineButtons = new();
    readonly Button _fightersButton = new() { Text = "Fighters", CornerRadius = 12, Padding = new Thickness(8, 10) };
    readonly Button _competitionsButton = new() { Text = "Tävlingar", CornerRadius = 12, Padding = new Thickness(8, 10) };
    readonly Picker _competitionSortPicker = new()
    {
        Title = "Sortera på", ItemsSource = new[] { "Datum", "Medaljpoäng" }, SelectedIndex = 0,
        TextColor = Colors.White
    };
    readonly Grid _competitionSortRow;
    ClubSummaryEntry? _club;
    ClubDetailsSnapshot _snapshot = new();
    bool _showCompetitions = true;
    ClubCompetitionSortMode _competitionSort = ClubCompetitionSortMode.Date;
    int _request;

    public ClubDetailsPage()
    {
        Title = "Klubb";
        NavigationUiService.UseBackButton(this);
        ToolbarItems.Add(new ToolbarItem("Klasser", null, async () =>
            await NavigationUiService.GoToAsync(nameof(RankingClassesPage), this)));

        var heading = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star) },
            ColumnSpacing = 12
        };
        var logoBorder = new Border { Content = _logo, BackgroundColor = Colors.White, Padding = 6,
            StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 14 } };
        heading.Add(logoBorder);
        _name.VerticalOptions = LayoutOptions.Center;
        heading.Add(_name, 1, 0);

        _genderFilter.SelectionChanged += async (_, _) => await RefreshDataAsync();
        _genderFilter.Margin = new Thickness(0, 0, 0, 6);

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
            var button = new Button { Text = text, Padding = new Thickness(10), Margin = new Thickness(0, 0, 6, 6) };
            FlexLayout.SetShrink(button, 0);
            button.Clicked += async (_, _) =>
            {
                ResultsFilterSettings.Discipline = discipline;
                await RefreshDataAsync();
            };
            _disciplineButtons.Add(discipline, button);
            disciplines.Add(button);
        }

        var tabs = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto) },
            ColumnSpacing = 4, Margin = new Thickness(6, 0, 0, 6)
        };
        tabs.Add(_competitionsButton);
        tabs.Add(_fightersButton, 1, 0);
        _fightersButton.Clicked += (_, _) => { _showCompetitions = false; RenderResults(); };
        _competitionsButton.Clicked += (_, _) => { _showCompetitions = true; RenderResults(); };

        // Keep both groups together, wrapping the tabs when enlarged text needs more room.
        var genderAndTabs = new FlexLayout
        {
            Direction = FlexDirection.Row, Wrap = FlexWrap.Wrap, AlignItems = FlexAlignItems.Center,
            AlignContent = FlexAlignContent.Start, JustifyContent = FlexJustify.SpaceBetween
        };
        FlexLayout.SetShrink(_genderFilter, 0);
        FlexLayout.SetShrink(tabs, 0);
        genderAndTabs.Add(_genderFilter);
        genderAndTabs.Add(tabs);

        _competitionSortRow = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star) },
            ColumnSpacing = 10
        };
        _competitionSortRow.Add(new Label { Text = "Sortera på:", TextColor = Colors.White, VerticalOptions = LayoutOptions.Center });
        _competitionSortRow.Add(_competitionSortPicker, 1, 0);
        _competitionSortPicker.SelectedIndexChanged += (_, _) =>
        {
            _competitionSort = _competitionSortPicker.SelectedIndex == 1
                ? ClubCompetitionSortMode.MedalPoints : ClubCompetitionSortMode.Date;
            RenderResults();
        };

        _results.ItemsLayout = new LinearItemsLayout(ItemsLayoutOrientation.Vertical) { ItemSpacing = 8 };
        _results.ItemTemplate = new DataTemplate(CreateResultCard);
        _results.EmptyView = new Label { Text = "Inga resultat för valda filter.", TextColor = Colors.White,
            Margin = new Thickness(16), HorizontalTextAlignment = TextAlignment.Center };
        _results.SelectionChanged += OnResultSelected;

        var filters = new VerticalStackLayout { Spacing = 8, Children = { heading, genderAndTabs, disciplines, _competitionSortRow } };
        var statusRow = new Grid
        {
            ColumnSpacing = 8,
            ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star) }
        };
        statusRow.Add(_busy);
        statusRow.Add(_status, 1, 0);
        // Let the filters scroll with the list so short screens and enlarged text remain usable.
        _results.Header = new VerticalStackLayout
        {
            Spacing = 8, Margin = new Thickness(0, 0, 0, 10), Children = { filters, statusRow }
        };
        var root = new Grid { Padding = new Thickness(10), Children = { _results } };
        Content = new AppBackgroundView { Children = { root } };
        RefreshStyles();
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (!query.TryGetValue("clubKey", out var value)) return;
        var club = NavigationParameterStore.Take<ClubSummaryEntry>(value?.ToString());
        if (club == null) return;
        _club = club;
        Title = _name.Text = _club.Name;
        _logo.Source = _club.ImageSource;
        SemanticProperties.SetDescription(_logo, $"Klubbild för {_club.Name}");
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
        base.OnDisappearing();
    }

    public Task InvalidateDataAsync() => Task.CompletedTask;

    public async Task RefreshDataAsync()
    {
        var token = _lifetime.Token;
        if (_club == null || token.IsCancellationRequested) return;
        var request = ++_request;
        var clubId = _club.Id;
        var gender = _genderFilter.SelectedGender;
        var discipline = ResultsFilterSettings.Discipline;
        var classes = RankingClassFilterService.GetEnabledClasses();
        RefreshStyles();
        _busy.IsVisible = _busy.IsRunning = true;
        _status.Text = "Sammanställer klubbresultat...";
        try
        {
            var competitors = await CompetitorRepository.GetAllAsync().WaitAsync(token);
            var snapshot = await Task.Run(() => ClubDetailsService.Build(competitors, clubId, gender, discipline, classes), token);
            if (!_lifetime.IsCurrent(token) || request != _request) return;
            _snapshot = snapshot;
            RenderResults();
            _status.Text = classes.Count == 0 ? "Inga klasser valda. Välj klasser via Klasser."
                : $"{snapshot.Fighters.Count} fighters · {snapshot.Competitions.Count} tävlingar · {snapshot.TotalPoints} medaljpoäng";
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex)
        {
            AppDiagnostics.Log(ex, "Ladda klubbresultat");
            if (_lifetime.IsCurrent(token) && request == _request) _status.Text = "Kunde inte läsa klubbresultaten.";
        }
        finally
        {
            if (_lifetime.IsCurrent(token) && request == _request) _busy.IsVisible = _busy.IsRunning = false;
        }
    }

    void RenderResults()
    {
        RefreshStyles();
        _results.ItemsSource = _showCompetitions
            ? ClubDetailsService.SortCompetitions(_snapshot.Competitions, _competitionSort).Select(competition => new ResultRow
            {
                Title = competition.Name,
                Subtitle = $"{competition.Date:yyyy-MM-dd} · {competition.City} · {competition.FighterCount} fighters",
                Points = competition.TotalPoints, Gold = competition.GoldCount, Silver = competition.SilverCount,
                Bronze = competition.BronzeCount, Competition = competition
            }).ToList()
            : _snapshot.Fighters.Select(fighter => new ResultRow
            {
                Title = fighter.FullName,
                Subtitle = fighter.Competitor.CurrentAgeClass is { } ageClass
                    ? RankingClassFilterService.ToSingularDisplayName(ageClass) : "Åldersklass saknas",
                Points = fighter.Score, Gold = fighter.GoldCount, Silver = fighter.SilverCount,
                Bronze = fighter.BronzeCount, Fighter = fighter.Competitor
            }).ToList();
    }

    void RefreshStyles()
    {
        foreach (var (discipline, button) in _disciplineButtons)
            StyleFilter(button, discipline == ResultsFilterSettings.Discipline);
        StyleFilter(_fightersButton, !_showCompetitions);
        StyleFilter(_competitionsButton, _showCompetitions);
        _competitionSortRow.IsVisible = _showCompetitions;
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

    async void OnResultSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not ResultRow row) return;
        _results.SelectedItem = null;
        if (row.Fighter is { } fighter)
            await NavigationUiService.GoToWithParameterAsync(nameof(CompetitorDetailsPage), "competitorKey", fighter, this);
        else if (_club is { } club && row.Competition is { } entry)
        {
            var token = _lifetime.Token;
            try
            {
                var catalog = await CompetitionCatalogRepository.GetCatalogAsync().WaitAsync(token);
                var competition = CompetitionCatalogService.FindCurrent(catalog, new CompetitionCatalogEntry
                {
                    Key = string.Join("|", entry.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), entry.Name, entry.City),
                    CompetitionDate = entry.Date, CompetitionName = entry.Name, CompetitionCity = entry.City
                });
                if (competition != null && _lifetime.IsCurrent(token))
                    await NavigationUiService.GoToWithParameterAsync(nameof(ClubCompetitionDetailsPage), "clubCompetitionKey",
                        new ClubCompetitionNavigation(club, competition), this);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            catch (Exception ex) { AppDiagnostics.Log(ex, "Öppna klubbens tävling"); }
        }
    }

    static object CreateResultCard()
    {
        var title = new Label { FontAttributes = FontAttributes.Bold, FontSize = 17, LineBreakMode = LineBreakMode.WordWrap };
        title.SetBinding(Label.TextProperty, nameof(ResultRow.Title));
        var points = new Label { FontAttributes = FontAttributes.Bold, FontSize = 18, VerticalOptions = LayoutOptions.Center };
        points.SetBinding(Label.TextProperty, new Binding(nameof(ResultRow.Points), stringFormat: "{0} p"));
        var subtitle = new Label { FontSize = 12, LineBreakMode = LineBreakMode.WordWrap };
        subtitle.SetBinding(Label.TextProperty, nameof(ResultRow.Subtitle));
        subtitle.SetAppThemeColor(Label.TextColorProperty, ThemeColors.SecondaryTextLight, ThemeColors.SecondaryTextDark);
        var medals = new HorizontalStackLayout { Spacing = 6 };
        foreach (var (image, property) in new[] { ("gold.png", nameof(ResultRow.Gold)), ("silver.png", nameof(ResultRow.Silver)), ("bronze.png", nameof(ResultRow.Bronze)) })
        {
            medals.Add(new Image { Source = image, WidthRequest = 18, HeightRequest = 18, Aspect = Aspect.AspectFit });
            var count = new Label { FontSize = 12, VerticalOptions = LayoutOptions.Center };
            count.SetBinding(Label.TextProperty, property);
            medals.Add(count);
        }
        var grid = new Grid
        {
            Padding = new Thickness(12), ColumnSpacing = 8, RowSpacing = 6,
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
            RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto) }
        };
        grid.Add(title);
        grid.Add(points, 1, 0);
        grid.Add(subtitle, 0, 1);
        Grid.SetColumnSpan(subtitle, 2);
        grid.Add(medals, 0, 2);
        Grid.SetColumnSpan(medals, 2);
        var card = new Border { Content = grid, StrokeThickness = 1, StrokeShape = new RoundRectangle { CornerRadius = 14 } };
        card.SetAppThemeColor(BackgroundColorProperty, ThemeColors.CardBackgroundLight, ThemeColors.CardBackgroundDark);
        card.SetAppTheme(Border.StrokeProperty, new SolidColorBrush(ThemeColors.CardStrokeLight), new SolidColorBrush(ThemeColors.CardStrokeDark));
        return card;
    }

    sealed class ResultRow
    {
        public string Title { get; init; } = string.Empty;
        public string Subtitle { get; init; } = string.Empty;
        public int Points { get; init; }
        public int Gold { get; init; }
        public int Silver { get; init; }
        public int Bronze { get; init; }
        public Competitor? Fighter { get; init; }
        public ClubCompetitionEntry? Competition { get; init; }
    }
}
