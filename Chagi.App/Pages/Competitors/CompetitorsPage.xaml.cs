using ITF_Taekwondo_Gradering.Logic;
using ITF_Taekwondo_Gradering.Models.Ranking;
using Microsoft.Maui.Controls.Shapes;

namespace ITF_Taekwondo_Gradering
{
    public partial class CompetitorsPage : ContentPage
    {
        List<Competitor> _competitors = new();
        GenderFilterMode _selectedGender = ResultsFilterSettings.DefaultGender;
        CompetitionDisciplineE _selectedDiscipline = ResultsFilterSettings.Discipline;
        bool _hasLoaded;
        bool _isRefreshing;
        int _lastRepositoryChangeVersion = -1;
        int _lastRankingClassChangeVersion = -1;
        readonly PageLifetime _lifetime = new();

        public CompetitorsPage()
        {
            InitializeComponent();
        }

        protected async override void OnAppearing()
        {
            base.OnAppearing();
            _lifetime.Activate();
            await ResultsDataRefreshService.AppearingAsync(this);
        }

        public async Task RefreshDataAsync()
        {
            var token = _lifetime.Token;
            if (_isRefreshing)
                return;

            try
            {
                var filtersChanged = _selectedGender != GenderFilter.SelectedGender ||
                    _selectedDiscipline != ResultsFilterSettings.Discipline;
                _selectedGender = GenderFilter.SelectedGender;
                _selectedDiscipline = ResultsFilterSettings.Discipline;
                RefreshFilterStyles();
                if (!_hasLoaded)
                {
                    SetBusyState(true, "Laddar topplistan...", "Laddar topplistan...");

                    _competitors = await CompetitorRepository.GetAllAsync().WaitAsync(token);

                    StatusLabel.Text = _competitors.Count > 0
                        ? $"{_competitors.Count} fighters hittade ..."
                        : "Inga fighters funna";

                    RefreshFilterStyles();
                    await RefreshSectionsAsync();
                    if (_lifetime.IsCurrent(token)) _hasLoaded = true;
                    return;
                }

                if (!filtersChanged && !NeedsRefreshOnReappear())
                    return;

                if (_lastRepositoryChangeVersion != CompetitorRepository.GetChangeVersion())
                {
                    _competitors = await CompetitorRepository.GetAllAsync().WaitAsync(token);
                }

                await RefreshSectionsAsync();
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            catch (Exception ex)
            {
                AppDiagnostics.Log(ex, "Ladda topplistan");
                if (_lifetime.IsCurrent(token)) StatusLabel.Text = "Fel vid inläsning";
            }
            finally
            {
                if (_lifetime.IsCurrent(token)) SetBusyState(false);
            }
        }

        protected override void OnDisappearing()
        {
            ResultsDataRefreshService.Disappearing(this);
            _lifetime.Deactivate();
            _isRefreshing = false;
            SetBusyState(false);
            base.OnDisappearing();
        }

        async void OnGenderChanged(object? sender, EventArgs e)
        {
            _selectedGender = GenderFilter.SelectedGender;
            RefreshFilterStyles();
            await RefreshSectionsAsync();
        }

        async void OnSearchClicked(object? sender, EventArgs e)
        {
            await NavigationUiService.GoToAsync($"{nameof(CompetitorSearchPage)}?focusSearch=true");
        }

        async void OnClassesClicked(object? sender, EventArgs e)
        {
            await NavigationUiService.GoToAsync(
                nameof(RankingClassesPage));
        }

        async void OnBrowseAllClicked(object? sender, EventArgs e)
        {
            await NavigationUiService.GoToAsync(nameof(AllCompetitorsPage));
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

            await OpenCompetitorDetailsAsync(entry);
        }

        async void OnAllClicked(object? sender, EventArgs e)
        {
            ResultsFilterSettings.Discipline = _selectedDiscipline = CompetitionDisciplineE.Alla;
            RefreshFilterStyles();
            await RefreshSectionsAsync();
        }

        async void OnTulClicked(object? sender, EventArgs e)
        {
            ResultsFilterSettings.Discipline = _selectedDiscipline = CompetitionDisciplineE.Tul;
            RefreshFilterStyles();
            await RefreshSectionsAsync();
        }

        async void OnSparringClicked(object? sender, EventArgs e)
        {
            ResultsFilterSettings.Discipline = _selectedDiscipline = CompetitionDisciplineE.Sparring;
            RefreshFilterStyles();
            await RefreshSectionsAsync();
        }

        async void OnSpecClicked(object? sender, EventArgs e)
        {
            ResultsFilterSettings.Discipline = _selectedDiscipline = CompetitionDisciplineE.Specialtekniker;
            RefreshFilterStyles();
            await RefreshSectionsAsync();
        }

        async void OnKrossClicked(object? sender, EventArgs e)
        {
            ResultsFilterSettings.Discipline = _selectedDiscipline = CompetitionDisciplineE.Kross;
            RefreshFilterStyles();
            await RefreshSectionsAsync();
        }

        void RefreshFilterStyles()
        {

            SetTabButtonState(AllButton, _selectedDiscipline == CompetitionDisciplineE.Alla);
            SetTabButtonState(TulButton, _selectedDiscipline == CompetitionDisciplineE.Tul);
            SetTabButtonState(SparringButton, _selectedDiscipline == CompetitionDisciplineE.Sparring);
            SetTabButtonState(SpecButton, _selectedDiscipline == CompetitionDisciplineE.Specialtekniker);
            SetTabButtonState(KrossButton, _selectedDiscipline == CompetitionDisciplineE.Kross);
        }

        void SetTabButtonState(Button button, bool isSelected)
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

        async Task RefreshSectionsAsync()
        {
            var token = _lifetime.Token;
            if (_isRefreshing || token.IsCancellationRequested)
                return;

            _isRefreshing = true;
            SetBusyState(true, StatusLabel.Text, "Uppdaterar topplistan...");
            try
            {
                await Task.Yield();
                var sections = await Task.Run(() => BuildSections(), token).WaitAsync(token);
                token.ThrowIfCancellationRequested();
                RenderSections(sections);
                StatusLabel.Text = RankingClassFilterService.GetEnabledClasses().Count == 0
                    ? "Inga klasser valda. Välj klasser via Klasser."
                    : _competitors.Count > 0
                    ? $"{_competitors.Count} fighters hittade ..."
                    : "Inga fighters funna";
                _lastRepositoryChangeVersion = CompetitorRepository.GetChangeVersion();
                _lastRankingClassChangeVersion = RankingClassFilterService.GetChangeVersion();
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            catch (Exception ex)
            {
                AppDiagnostics.Log(ex, "Uppdatera topplistan");
                if (_lifetime.IsCurrent(token)) StatusLabel.Text = "Kunde inte uppdatera topplistan.";
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

        bool NeedsRefreshOnReappear()
        {
            return _lastRepositoryChangeVersion != CompetitorRepository.GetChangeVersion() ||
                   _lastRankingClassChangeVersion != RankingClassFilterService.GetChangeVersion();
        }

        void SetBusyState(bool isBusy, string? statusText = null, string? overlayText = null)
        {
            BusyIndicator.IsVisible = isBusy;
            BusyIndicator.IsRunning = isBusy;
            LoadingOverlay.IsVisible = isBusy;
            OverlayIndicator.IsVisible = isBusy;
            OverlayIndicator.IsRunning = isBusy;

            if (statusText != null)
            {
                StatusLabel.Text = statusText;
            }

            if (overlayText != null)
            {
                OverlayLabel.Text = overlayText;
            }

            SetControlState(!isBusy);
        }

        void SetControlState(bool isEnabled)
        {
            GenderFilter.IsEnabled = isEnabled;
            SearchButton.IsEnabled = isEnabled;
            BrowseAllButton.IsEnabled = isEnabled;
            AllButton.IsEnabled = isEnabled;
            TulButton.IsEnabled = isEnabled;
            SparringButton.IsEnabled = isEnabled;
            SpecButton.IsEnabled = isEnabled;
            KrossButton.IsEnabled = isEnabled;
        }

        List<RankingSection> BuildSections()
        {
            return _selectedGender switch
            {
                GenderFilterMode.Male => RankingService.BuildSections(_competitors, showMale: true, _selectedDiscipline),
                GenderFilterMode.Female => RankingService.BuildSections(_competitors, showMale: false, _selectedDiscipline),
                _ => BuildAllGenderSections(),
            };
        }

        List<RankingSection> BuildAllGenderSections()
        {
            var maleSections = RankingService.BuildSections(_competitors, showMale: true, _selectedDiscipline);
            var femaleSections = RankingService.BuildSections(_competitors, showMale: false, _selectedDiscipline);

            var mergedSections = new List<RankingSection>();

            foreach (var group in maleSections.Concat(femaleSections)
                .GroupBy(section => (section.AgeClass, section.BeltGroup))
                .OrderBy(group => group.Key.AgeClass).ThenBy(group => group.Key.BeltGroup))
            {
                var mergedEntries = group.SelectMany(section => section.Entries).ToList();

                mergedEntries = RankingPositionService
                    .OrderByScoreAndMedals(
                        mergedEntries,
                        e => e.Score,
                        e => e.GoldCount,
                        e => e.SilverCount,
                        e => e.BronzeCount)
                    .ThenBy(e => e.FullName, SwedishNameComparer.Instance)
                    .ThenBy(e => e.Team, SwedishNameComparer.Instance)
                    .ToList();

                RankingPositionService.ApplySharedPositions(
                    mergedEntries,
                    (left, right) => RankingPositionService.HaveSameScoreAndMedals(
                        left,
                        right,
                        e => e.Score,
                        e => e.GoldCount,
                        e => e.SilverCount,
                        e => e.BronzeCount),
                    (entry, position) => entry.Position = position,
                    (entry, label) => entry.PositionLabel = label);

                mergedSections.Add(new RankingSection
                {
                    AgeClass = group.Key.AgeClass,
                    BeltGroup = group.Key.BeltGroup,
                    Title = group.First().Title,
                    Entries = mergedEntries
                });
            }

            return mergedSections.Where(section => section.Entries.Count > 0).ToList();
        }

        void RenderSections(List<RankingSection> sections)
        {
            RankingView.ItemsSource = sections
                .Where(section => section.Entries.Count > 0)
                .Select(section => new RankingSectionGroup(section.Title, section.Entries))
                .ToList();
        }

        async Task OpenCompetitorDetailsAsync(RankedCompetitorEntry entry)
        {
            if (entry.Competitor == null)
                return;

            await NavigationUiService.GoToWithParameterAsync(nameof(CompetitorDetailsPage), "competitorKey", entry.Competitor, this);
        }

        View CreateMedalSummary(RankedCompetitorEntry entry)
        {
            var medals = new HorizontalStackLayout
            {
                Spacing = 10,
                HorizontalOptions = LayoutOptions.End,
                VerticalOptions = LayoutOptions.Start
            };

            medals.Children.Add(CreateMedalItem("gold.png", entry.GoldCount));
            medals.Children.Add(CreateMedalItem("silver.png", entry.SilverCount));
            medals.Children.Add(CreateMedalItem("bronze.png", entry.BronzeCount));

            return medals;
        }

        Button CreateFavoriteButton(RankedCompetitorEntry entry)
        {
            var button = new Button
            {
                Text = entry.IsFavorite ? "★" : "☆",
                WidthRequest = 36,
                HeightRequest = 36,
                Padding = 0,
                FontSize = 20,
                BackgroundColor = Colors.Transparent,
                TextColor = Color.FromArgb("#D4A017"),
                CornerRadius = 18
            };

            button.Clicked += async (_, _) =>
            {
                await CompetitorRepository.SetFavoriteAsync(entry.Competitor, !entry.Competitor.IsFavorite);
                await RefreshSectionsAsync();
            };

            button.SetAppThemeColor(Button.TextColorProperty, Color.FromArgb("#D4A017"), Color.FromArgb("#F0B429"));

            return button;
        }

        VerticalStackLayout CreateMedalItem(string imageSource, int count)
        {
            const double iconSize = 18;

            return new VerticalStackLayout
            {
                Spacing = 2,
                HorizontalOptions = LayoutOptions.Center,
                Children =
                {
                    new Border
                    {
                        WidthRequest = iconSize,
                        HeightRequest = iconSize,
                        Padding = 0,
                        StrokeThickness = 0,
                        BackgroundColor = Colors.Transparent,
                        StrokeShape = new Ellipse(),
                        HorizontalOptions = LayoutOptions.Center,
                        Content = new Image
                        {
                            Source = imageSource,
                            HeightRequest = iconSize,
                            WidthRequest = iconSize,
                            Aspect = Aspect.AspectFill,
                            HorizontalOptions = LayoutOptions.Center,
                            VerticalOptions = LayoutOptions.Center,
                            Clip = new EllipseGeometry
                            {
                                Center = new Point(iconSize / 2, iconSize / 2),
                                RadiusX = iconSize / 2,
                                RadiusY = iconSize / 2
                            }
                        }
                    },
                    new Label
                    {
                        Text = count.ToString(),
                        FontSize = 11,
                        HorizontalTextAlignment = TextAlignment.Center
                    }
                }
            };
        }

    }

}
