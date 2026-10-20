using ITF_Taekwondo_Gradering.Logic;
using ITF_Taekwondo_Gradering.Models.Clubs;

namespace ITF_Taekwondo_Gradering;

public partial class ClubsPage : ContentPage
{
    IReadOnlyList<ClubSummaryEntry> _clubs = Array.Empty<ClubSummaryEntry>();
    bool _hasLoaded;
    bool _isLoading;
    int _lastRepositoryChangeVersion = -1;

    public ClubsPage()
    {
        InitializeComponent();
        NavigationUiService.UseBackButton(this);
        SortPicker.ItemsSource = new[] { "Medaljpoäng", "Namn" };
        SortPicker.SelectedIndex = 1;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await ResultsDataRefreshService.AppearingAsync(this);
    }

    public async Task RefreshDataAsync()
    {
        var repositoryChangeVersion = CompetitorRepository.GetChangeVersion();
        if (_isLoading || (_hasLoaded && _lastRepositoryChangeVersion == repositoryChangeVersion))
            return;

        _lastRepositoryChangeVersion = repositoryChangeVersion;
        await LoadClubsAsync();
    }

    protected override void OnDisappearing()
    {
        ResultsDataRefreshService.Disappearing(this);
        base.OnDisappearing();
    }

    async void OnClubSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not ClubSummaryEntry club) return;
        ClubsView.SelectedItem = null;
        await NavigationUiService.GoToWithParameterAsync(nameof(ClubDetailsPage), "clubKey", club, this);
    }

    void OnSortPickerChanged(object? sender, EventArgs e)
    {
        ApplySort();
    }

    void ApplySort()
    {
        ClubsView.ItemsSource = SortPicker.SelectedIndex == 1
            ? _clubs.OrderBy(club => club.Name, SwedishNameComparer.Instance).ToList()
            : _clubs;
    }

    async Task LoadClubsAsync()
    {
        _isLoading = true;
        SetBusyState(true, "Laddar klubbar...", "Sammanställer klubbresultat...");

        try
        {
            await Task.Yield();

            var clubs = await ClubStatisticsRepository.GetClubSummariesAsync();
            if (!ResultsDataRefreshService.IsVisible(this)) return;
            _clubs = clubs;
            ApplySort();
            StatusLabel.Text = $"{clubs.Count} klubbar";
            _hasLoaded = true;
        }
        finally
        {
            _isLoading = false;
            SetBusyState(false);
        }
    }

    void SetBusyState(bool isBusy, string? statusText = null, string? overlayText = null)
    {
        BusyIndicator.IsVisible = isBusy;
        BusyIndicator.IsRunning = isBusy;
        LoadingOverlay.IsVisible = isBusy;
        OverlayIndicator.IsVisible = isBusy;
        OverlayIndicator.IsRunning = isBusy;
        ClubsView.IsEnabled = !isBusy;
        SortPicker.IsEnabled = !isBusy;

        if (statusText != null)
            StatusLabel.Text = statusText;

        if (overlayText != null)
            OverlayLabel.Text = overlayText;
    }
}
