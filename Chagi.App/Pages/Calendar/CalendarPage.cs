using ITF_Taekwondo_Gradering.WebConnection;

namespace ITF_Taekwondo_Gradering;

public class CalendarPage : ContentPage
{
    readonly ActivityIndicator _activityIndicator;
    readonly Label _statusLabel;
    readonly VerticalStackLayout _host;
    readonly ScrollView _scrollView;
    readonly VerticalStackLayout _content;
    View? _pendingScrollTarget;
    bool _scrollQueued;
    bool _isLoading;

    public CalendarPage()
    {
        Title = "Kalender";

        _activityIndicator = new ActivityIndicator
        {
            IsRunning = true,
            Color = Colors.White,
            HorizontalOptions = LayoutOptions.Center
        };

        _statusLabel = new Label
        {
            Text = "Laddar kalender...",
            TextColor = Colors.White,
            HorizontalTextAlignment = TextAlignment.Center
        };

        _host = new VerticalStackLayout
        {
            Spacing = 8
        };

        _content = new VerticalStackLayout
        {
            Padding = 16,
            Spacing = 14,
            Children =
            {
                _activityIndicator,
                _statusLabel,
                _host
            }
        };
        _scrollView = new ScrollView { Content = _content };
        _content.SizeChanged += (_, _) => QueueScrollToUpcomingEvent();
        _host.SizeChanged += (_, _) => QueueScrollToUpcomingEvent();
        _scrollView.SizeChanged += (_, _) => QueueScrollToUpcomingEvent();
        Content = new AppBackgroundView { Children = { _scrollView } };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_isLoading)
            return;

        await LoadCalendarAsync();
    }

    async Task LoadCalendarAsync()
    {
        try
        {
            _isLoading = true;
            SetLoadingState();

            var result = await CalendarSyncService.GetCalendarAsync();
            await RenderCalendarAsync(result);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
            _activityIndicator.IsRunning = false;
            _activityIndicator.IsVisible = false;
            _statusLabel.Text = "Kunde inte läsa kalendern.";
        }
        finally
        {
            _activityIndicator.IsRunning = _activityIndicator.IsVisible = false;
            _isLoading = false;
            QueueScrollToUpcomingEvent();
        }
    }

    void SetLoadingState()
    {
        _pendingScrollTarget = null;
        _content.Padding = 16;
        _activityIndicator.IsVisible = true;
        _activityIndicator.IsRunning = true;
        _statusLabel.Text = "Laddar kalender...";
        _host.Children.Clear();
    }

    async Task RenderCalendarAsync(CalendarSyncResult result)
    {
        _host.Children.Clear();

        var entries = await Task.Run(() => result.Entries
            .OrderBy(entry => entry.SortDate)
            .ThenBy(entry => entry.Description)
            .ToList());

        if (entries.Count == 0)
        {
            _statusLabel.Text = string.IsNullOrWhiteSpace(result.ErrorMessage)
                ? "Inga kalenderhändelser hittades."
                : result.ErrorMessage;
            return;
        }

        _statusLabel.Text = result.LoadedFromCache
            ? $"{entries.Count} kalenderhändelser från sparad kalender"
            : $"{entries.Count} kalenderhändelser";

        var today = DateTime.Today;
        var currentYear = 0;
        var currentMonthSection = string.Empty;
        foreach (var entry in entries)
        {
            if (entry.Year != currentYear)
            {
                currentYear = entry.Year;
                currentMonthSection = string.Empty;
                _host.Children.Add(BuildYearHeader(entry.Year.ToString()));
            }

            if (!string.Equals(entry.MonthName, currentMonthSection, StringComparison.Ordinal))
            {
                currentMonthSection = entry.MonthName;
                _host.Children.Add(BuildSectionHeader(entry.MonthName, entry.Year == today.Year && entry.Month == today.Month));
            }

            var isPast = GetEffectiveEndDate(entry) < today;
            var row = BuildEventRow(entry, isPast);
            _host.Children.Add(row);
            await Task.Delay(1);
            if (!isPast && _pendingScrollTarget is null)
            {
                _pendingScrollTarget = row;
                row.SizeChanged += (_, _) => QueueScrollToUpcomingEvent();
            }
        }

        QueueScrollToUpcomingEvent();
    }

    void QueueScrollToUpcomingEvent()
    {
        if (_isLoading || _pendingScrollTarget is null || _scrollQueued)
            return;

        _scrollQueued = true;
        Dispatcher.Dispatch(async () =>
        {
            _scrollQueued = false;
            var target = _pendingScrollTarget;
            if (target is null || target.Height <= 0 || _host.Height <= 0 || _scrollView.Height <= 0)
                return;

            // Leave enough space after the last event to align the target at the top.
            var bottomPadding = Math.Max(16, _scrollView.Height - (_host.Height - target.Y));
            if (Math.Abs(_content.Padding.Bottom - bottomPadding) > 0.5)
            {
                _content.Padding = new Thickness(16, 16, 16, bottomPadding);
                return; // Scroll after the updated content has been laid out.
            }

            _pendingScrollTarget = null;
            try
            {
                await _scrollView.ScrollToAsync(target, ScrollToPosition.Start, animated: false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
            }
        });
    }

    static View BuildYearHeader(string text)
    {
        var label = new Label
        {
            Text = text,
            FontAttributes = FontAttributes.Bold,
            FontSize = 22,
            TextColor = Colors.White,
            Margin = new Thickness(0, 4, 0, 2)
        };
        return label;
    }

    static View BuildSectionHeader(string text, bool isCurrentMonth)
    {
        return new Border
        {
            StrokeThickness = 0,
            BackgroundColor = isCurrentMonth ? Color.FromArgb("#FFE08A") : Color.FromArgb("#E2E8F0"),
            Padding = new Thickness(10, 6),
            Content = new Label
            {
                Text = text,
                FontAttributes = FontAttributes.Bold,
                FontSize = 14
            }
        }.Assign(border =>
        {
            if (isCurrentMonth)
                border.SetAppThemeColor(Border.BackgroundColorProperty, Color.FromArgb("#FFE08A"), Color.FromArgb("#665C2A"));
            else
                border.SetAppThemeColor(Border.BackgroundColorProperty, ThemeColors.SurfaceLight, ThemeColors.SurfaceDark);
        });
    }

    static View BuildEventRow(CalendarEntry entry, bool isPast)
    {
        var dateLabel = new Label
        {
            Text = entry.DayText,
            FontAttributes = FontAttributes.Bold,
            FontSize = 14,
            WidthRequest = 58,
            LineBreakMode = LineBreakMode.WordWrap
        };
        dateLabel.SetAppThemeColor(Label.TextColorProperty, ThemeColors.PrimaryTextLight, ThemeColors.PrimaryTextDark);

        var descriptionLabel = new Label
        {
            Text = entry.Description,
            FontSize = 14,
            LineBreakMode = LineBreakMode.WordWrap
        };
        descriptionLabel.SetAppThemeColor(Label.TextColorProperty, ThemeColors.PrimaryTextLight, ThemeColors.PrimaryTextDark);

        if (isPast)
        {
            dateLabel.TextDecorations = TextDecorations.Strikethrough;
            descriptionLabel.TextDecorations = TextDecorations.Strikethrough;
            dateLabel.Opacity = 0.48;
            descriptionLabel.Opacity = 0.48;
        }

        var row = new Grid
        {
            ColumnSpacing = 10,
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star }
            },
            Children =
            {
                dateLabel,
                descriptionLabel
            }
        };
        Grid.SetColumn(descriptionLabel, 1);

        return CompetitionUiHelpers.CreateCard(row, new Thickness(10, 8));
    }

    static DateTime GetEffectiveEndDate(CalendarEntry entry)
    {
        return entry.EndDate == default ? entry.SortDate : entry.EndDate;
    }
}
