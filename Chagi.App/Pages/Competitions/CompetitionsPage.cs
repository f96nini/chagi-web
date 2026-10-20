using ITF_Taekwondo_Gradering.Logic;

namespace ITF_Taekwondo_Gradering;

public partial class CompetitionsPage : ContentPage
{
    readonly ActivityIndicator _activityIndicator;
    readonly Label _statusLabel;
    readonly VerticalStackLayout _host;
    IReadOnlyList<CompetitionCatalogEntry>? _competitions;
    bool _hasLoaded;
    bool _isLoading;
    int _lastRepositoryChangeVersion = -1;
    readonly PageLifetime _lifetime = new();

    public CompetitionsPage()
    {
        Title = "Tävlingar";

        _activityIndicator = new ActivityIndicator
        {
            IsRunning = true,
            Color = Colors.White,
            HorizontalOptions = LayoutOptions.Center
        };

        _statusLabel = new Label
        {
            Text = "Laddar tävlingar...",
            TextColor = Colors.White,
            HorizontalTextAlignment = TextAlignment.Center
        };

        _host = new VerticalStackLayout
        {
            Spacing = 12
        };

        var scrollView = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 16,
                Spacing = 14,
                Children =
                {
                    _activityIndicator,
                    _statusLabel,
                    _host
                }
            }
        };
        Content = new AppBackgroundView { Children = { scrollView } };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _lifetime.Activate();
        await ResultsDataRefreshService.AppearingAsync(this);
    }

    public async Task RefreshDataAsync()
    {
        if (_isLoading)
            return;

        if (_hasLoaded && !NeedsRefreshOnReappear())
            return;

        await LoadCompetitionsAsync();
    }

    protected override void OnDisappearing()
    {
        ResultsDataRefreshService.Disappearing(this);
        _lifetime.Deactivate();
        _isLoading = false;
        _activityIndicator.IsRunning = false;
        base.OnDisappearing();
    }

    async Task LoadCompetitionsAsync()
    {
        var token = _lifetime.Token;
        if (_isLoading || token.IsCancellationRequested)
            return;

        try
        {
            _isLoading = true;
            SetLoadingState();

            _competitions = await CompetitionCatalogRepository.GetCatalogAsync().WaitAsync(token);
            _lastRepositoryChangeVersion = CompetitorRepository.GetChangeVersion();
            await RenderCompetitionsAsync(token);
            _hasLoaded = true;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex)
        {
            AppDiagnostics.Log(ex, "Ladda tävlingar");
            if (!_lifetime.IsCurrent(token)) return;
            _activityIndicator.IsRunning = false;
            _activityIndicator.IsVisible = false;
            _statusLabel.Text = "Kunde inte läsa tävlingar.";
        }
        finally
        {
            if (_lifetime.IsCurrent(token))
            {
                _activityIndicator.IsRunning = _activityIndicator.IsVisible = false;
                _isLoading = false;
            }
        }
    }

    bool NeedsRefreshOnReappear()
    {
        return _lastRepositoryChangeVersion != CompetitorRepository.GetChangeVersion();
    }

    View BuildCompetitionCard(CompetitionCatalogEntry competition)
    {
        var countLabel = new Label
        {
            Text = string.Join("  ",
                competition.DisciplineCounts.Select(count =>
                    $"{CompetitionUiHelpers.ToDisciplineLabel(count.Discipline)} {count.Count}")),
            FontSize = 13,
            LineBreakMode = LineBreakMode.WordWrap
        };
        countLabel.SetAppThemeColor(Label.TextColorProperty, ThemeColors.SecondaryTextLight, ThemeColors.SecondaryTextDark);

        var participantCount = competition.ParticipantCount;
        var participantLabel = new Label
        {
            Text = participantCount.HasValue ? $"Antal deltagare: {participantCount.Value}" : string.Empty,
            FontSize = 13,
            IsVisible = participantCount.HasValue
        };
        participantLabel.SetAppThemeColor(Label.TextColorProperty, ThemeColors.SecondaryTextLight, ThemeColors.SecondaryTextDark);

        var title = CompetitionUiHelpers.BuildCompetitionTitle(competition.CompetitionDate, competition.CompetitionName);
        if (!string.IsNullOrWhiteSpace(competition.CompetitionCity))
            title += $", {competition.CompetitionCity.Trim()}";

        var content = new VerticalStackLayout
        {
            Spacing = 8,
            Children =
            {
                new Label
                {
                    Text = title,
                    FontAttributes = FontAttributes.Bold,
                    LineBreakMode = LineBreakMode.WordWrap
                },
                participantLabel,
                countLabel
            }
        };

        var card = CompetitionUiHelpers.CreateCard(content);
        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) =>
        {
            await NavigationUiService.GoToWithParameterAsync(nameof(CompetitionDetailsPage), "competitionKey", competition, this);
        };
        card.GestureRecognizers.Add(tap);
        return card;
    }

    void SetLoadingState()
    {
        _activityIndicator.IsVisible = true;
        _activityIndicator.IsRunning = true;
        _statusLabel.Text = "Laddar tävlingar...";
        _host.Children.Clear();
    }

    static View BuildYearDivider(int year)
    {
        var yearLabel = new Label
        {
            Text = year == DateTime.MinValue.Year ? "Okänt år" : year.ToString(),
            FontFamily = "OpenSansSemibold",
            FontSize = 30,
            CharacterSpacing = 2,
            TextColor = Color.FromArgb("#A5D8FF"),
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalOptions = LayoutOptions.Center
        };
        SemanticProperties.SetHeadingLevel(yearLabel, SemanticHeadingLevel.Level1);

        var divider = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 14,
            Margin = new Thickness(0, 18, 0, 4)
        };
        foreach (var column in new[] { 0, 2 })
            divider.Add(new BoxView
            {
                HeightRequest = 1,
                Color = Color.FromArgb("#82C7F4"),
                Opacity = 0.6,
                VerticalOptions = LayoutOptions.Center
            }, column, 0);
        divider.Add(yearLabel, 1, 0);
        return divider;
    }

    async Task RenderCompetitionsAsync(CancellationToken token)
    {
        var competitions = _competitions ?? new List<CompetitionCatalogEntry>();

        _statusLabel.Text = competitions.Count > 0
            ? $"{competitions.Count} tävlingar hittade"
            : "Inga tävlingar hittades.";
        _host.Children.Clear();

        int? currentYear = null;
        foreach (var competition in competitions)
        {
            var year = competition.CompetitionDate.Year;
            if (currentYear != year)
            {
                _host.Children.Add(BuildYearDivider(year));
                currentYear = year;
            }
            _host.Children.Add(BuildCompetitionCard(competition));
            await Task.Delay(1, token);
        }
    }
}
