using Microsoft.Maui.Storage;
using System.Collections.Generic;

namespace ITF_Taekwondo_Gradering
{
    public partial class SettingsPage : ContentPage
    {
        private List<RadioButton> _radios = new();
        private bool _loadingSettings;

        public SettingsPage()
        {
            InitializeComponent();

            // collect radio buttons for easier management
            _radios = new List<RadioButton>
            {
                Radio10Kup1, Radio10Kup2,
                Radio9Kup1, Radio9Kup2,
                Radio8Kup1, Radio8Kup2,
                Radio7Kup1, Radio7Kup2,
                Radio6Kup1, Radio6Kup2,
                Radio5Kup1, Radio5Kup2
            };
            foreach (var radio in _radios)
            {
                radio.TextColor = Colors.White;
                RadioButtonAppearance.Apply(radio, Colors.White, Colors.White);
                radio.CheckedChanged += OnRankChanged;
            }
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            _loadingSettings = true;
            try
            {
                var saved = Preferences.Get("RankSelection", string.Empty);
                foreach (var radio in _radios)
                    radio.IsChecked = radio.Content?.ToString() == saved;
                ThemeSwitch.IsToggled = AppThemeManager.IsDarkThemeActive();
            }
            finally { _loadingSettings = false; }
        }

        private async void OnRankChanged(object? sender, CheckedChangedEventArgs e)
        {
            if (_loadingSettings || !e.Value || sender is not RadioButton radio) return;
            try
            {
                Preferences.Set("RankSelection", radio.Content?.ToString() ?? string.Empty);
            }
            catch (Exception ex)
            {
                AppDiagnostics.Log(ex, "Spara inställningar");
                await DisplayAlert("Fel", "Kunde inte spara inställningarna. Försök igen.", "OK");
            }
        }

        private async void OnThemeToggled(object? sender, ToggledEventArgs e)
        {
            if (_loadingSettings) return;
            try
            {
                var preference = e.Value ? "Dark" : "Light";
                AppThemeManager.SetThemePreference(preference);
                AppThemeManager.ApplyTheme(preference);
            }
            catch (Exception ex)
            {
                AppDiagnostics.Log(ex, "Spara tema");
                await DisplayAlert("Fel", "Kunde inte spara temat. Försök igen.", "OK");
            }
        }
    }
}
