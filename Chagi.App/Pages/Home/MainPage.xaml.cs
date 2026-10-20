namespace ITF_Taekwondo_Gradering
{
    public partial class MainPage : ContentPage
    {
        static bool _startupWarmupScheduled;

        public MainPage()
        {
            InitializeComponent();
            QuickButtonStyle.Apply(CompetitorsButton);
            QuickButtonStyle.Apply(CompetitionsButton);
            QuickButtonStyle.Apply(StudyButton);
            QuickButtonStyle.Apply(QuizButton);
            QuickButtonStyle.Apply(ClubsButton);
            QuickButtonStyle.Apply(CalendarButton);
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            UpdateSelectedBeltDisplay();
            ScheduleStartupWarmup();
        }

        void UpdateSelectedBeltDisplay()
        {
            var saved = Microsoft.Maui.Storage.Preferences.Get("RankSelection", string.Empty);
            if (string.IsNullOrEmpty(saved))
            {
                SelectedBeltLabel.Text = string.Empty;
                return;
            }

            SelectedBeltLabel.Text = saved;
            BeltDisplayHelper.ApplyTo(SelectedBeltView, saved);
        }

        private async void OnStudyClicked(object? sender, EventArgs e)
        {
            await NavigationUiService.GoToAsync(nameof(StudyPage));
        }

        private async void OnQuizClicked(object? sender, EventArgs e)
        {
            await NavigationUiService.GoToAsync("//quiz");
        }

        private async void OnCompetitorsClicked(object? sender, EventArgs e)
        {
            await NavigationUiService.GoToAsync("//competitors");
        }

        private async void OnCompetitionsClicked(object? sender, EventArgs e)
        {
            await NavigationUiService.GoToAsync("//competitions");
        }

        private async void OnClubsClicked(object? sender, EventArgs e)
        {
            await NavigationUiService.GoToAsync("//clubs");
        }

        private async void OnCalendarClicked(object? sender, EventArgs e)
        {
            await NavigationUiService.GoToAsync("//calendar");
        }

        private async void OnClearCacheClicked(object? sender, EventArgs e)
        {
            try
            {
                // Clear cached competitors and rebuild
                await PageWorkService.RunAsync(this, Logic.CompetitorRepository.ClearCacheAsync, "Bygger om cache...");
                await DisplayAlert("Rensa cache", "Cache rensad och omparsing klar.", "OK");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
                await DisplayAlert("Fel", "Kunde inte rensa cache.", "OK");
            }
        }

        void ScheduleStartupWarmup()
        {
            if (_startupWarmupScheduled)
                return;

            // Android debug startup has been sensitive to background parsing and Fast Deploy state.
            // Skip eager warmup there and load competitor data on demand from the destination pages instead.
            if (OperatingSystem.IsAndroid() && System.Diagnostics.Debugger.IsAttached)
                return;

            _startupWarmupScheduled = true;

            Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(300), () =>
            {
                _ = PageWorkService.RunAsync(this, async () =>
                {
                    try
                    {
                        await Logic.CompetitorRepository.InitializeAsync();
                        await WebConnection.CalendarSyncService.WarmupAsync();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(ex);
                    }
                }, "Förbereder data...");
            });
        }
    }
}
