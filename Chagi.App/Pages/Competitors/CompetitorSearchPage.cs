using ITF_Taekwondo_Gradering.Logic;
using ITF_Taekwondo_Gradering.Models.Ranking;
using Microsoft.Maui.Controls.Shapes;

namespace ITF_Taekwondo_Gradering;

public partial class CompetitorSearchPage : ContentPage, IQueryAttributable
{
    readonly List<Competitor> _allCompetitors = new();
    bool _focusSearch;
    readonly SearchBar _searchBar;
    readonly CollectionView _resultsView;
    readonly ActivityIndicator _busy = new() { HorizontalOptions = LayoutOptions.Center };
    int _filterVersion;
    bool _loading;

    public CompetitorSearchPage()
    {
        Title = "Sök";

        _searchBar = new SearchBar
        {
            Placeholder = "Sök efter fighter"
        };
        _searchBar.TextChanged += async (_, _) => await ApplyFilterAsync();

        _resultsView = new CollectionView
        {
            SelectionMode = SelectionMode.None,
            ItemTemplate = new DataTemplate(CreateResultTemplate)
        };

        Content = new Grid
        {
            Padding = 12,
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star)
            },
            RowSpacing = 10,
            Children =
            {
                _searchBar,
                _busy,
                _resultsView
            }
        };

        Grid.SetRow(_busy, 1);
        Grid.SetRow(_resultsView, 2);
    }

    public CompetitorSearchPage(IEnumerable<Competitor> competitors, bool focusSearch)
        : this()
    {
        _allCompetitors.AddRange(competitors);
        _focusSearch = focusSearch;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("focusSearch", out var focusValue) &&
            bool.TryParse(focusValue?.ToString(), out var focusSearch))
        {
            _focusSearch = focusSearch;
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await ResultsDataRefreshService.AppearingAsync(this);
    }

    protected override void OnDisappearing()
    {
        ResultsDataRefreshService.Disappearing(this);
        ++_filterVersion;
        base.OnDisappearing();
    }

    public async Task RefreshDataAsync()
    {
        if (_loading) return;
        _loading = true;
        try
        {
            if (_allCompetitors.Count == 0)
                _allCompetitors.AddRange(await PageWorkService.RunBackgroundAsync(this, CompetitorRepository.GetAllAsync));
            if (ResultsDataRefreshService.IsVisible(this)) await ApplyFilterAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
            await DisplayAlert("Fel", "Kunde inte läsa fighters.", "OK");
        }
        finally { _loading = false; }

        if (_focusSearch)
        {
            MainThread.BeginInvokeOnMainThread(() => _searchBar.Focus());
        }
    }

    async Task ApplyFilterAsync()
    {
        var version = ++_filterVersion;
        var query = (_searchBar.Text ?? string.Empty).Trim();
        var competitors = _allCompetitors.ToArray();
        _busy.IsVisible = _busy.IsRunning = true;
        try
        {
            // Debounce typing and discard older results.
            await Task.Delay(150);
            if (version != _filterVersion) return;
            var filtered = await Task.Run(() => competitors
                .Where(c => string.IsNullOrWhiteSpace(query) || c.FullName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    c.Team.Contains(query, StringComparison.OrdinalIgnoreCase))
                .OrderBy(c => c.FullName, SwedishNameComparer.Instance).ThenBy(c => c.Team, SwedishNameComparer.Instance).ToList());
            if (version == _filterVersion) _resultsView.ItemsSource = filtered;
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
        finally
        {
            if (version == _filterVersion) _busy.IsVisible = _busy.IsRunning = false;
        }
    }

    View CreateResultTemplate()
    {
        var starButton = new Button
        {
            WidthRequest = 44,
            HeightRequest = 44,
            Padding = 0,
            FontSize = 22,
            BackgroundColor = Colors.Transparent,
            TextColor = Color.FromArgb("#D4A017"),
            CornerRadius = 22
        };
        starButton.BindingContextChanged += (_, _) =>
        {
            if (starButton.BindingContext is Competitor competitor)
            {
                starButton.Text = competitor.IsFavorite ? "★" : "☆";
            }
        };
        starButton.Clicked += async (_, _) =>
        {
            if (starButton.BindingContext is not Competitor competitor)
                return;

            await CompetitorRepository.SetFavoriteAsync(competitor, !competitor.IsFavorite);
            starButton.Text = competitor.IsFavorite ? "★" : "☆";
            await ApplyFilterAsync();
        };

        var nameLabel = new Label
        {
            FontAttributes = FontAttributes.Bold,
            FontSize = 16
        };
        nameLabel.SetBinding(Label.TextProperty, nameof(Competitor.FullName));

        var teamLabel = new Label
        {
            FontSize = 13
        };
        teamLabel.SetAppThemeColor(Label.TextColorProperty, ThemeColors.SecondaryTextLight, ThemeColors.SecondaryTextDark);
        teamLabel.SetBinding(Label.TextProperty, nameof(Competitor.Team));

        var textStack = new VerticalStackLayout
        {
            Spacing = 2,
            Children =
            {
                nameLabel,
                teamLabel
            }
        };

        var row = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 10,
            Padding = new Thickness(10, 8)
        };
        row.Add(starButton, 0, 0);
        row.Add(textStack, 1, 0);

        var border = new Border
        {
            StrokeThickness = 1,
            Stroke = Color.FromArgb("#D9E2EC"),
            BackgroundColor = Color.FromArgb("#F8FAFC"),
            StrokeShape = new RoundRectangle { CornerRadius = 12 },
            Margin = new Thickness(0, 0, 0, 8),
            Content = row
        };
        border.SetAppThemeColor(Border.BackgroundColorProperty, ThemeColors.CardBackgroundLight, ThemeColors.CardBackgroundDark);
        border.SetAppTheme<Brush>(
            Border.StrokeProperty,
            new SolidColorBrush(ThemeColors.CardStrokeLight),
            new SolidColorBrush(ThemeColors.CardStrokeDark));

        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) =>
        {
            if (border.BindingContext is Competitor competitor)
            {
                await NavigationUiService.GoToWithParameterAsync(nameof(CompetitorDetailsPage), "competitorKey", competitor, this);
            }
        };
        border.GestureRecognizers.Add(tap);

        return border;
    }
}
