using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;

namespace ITF_Taekwondo_Gradering;

static class AppThemeManager
{
    private const string AppThemePreferenceKey = "AppTheme";
    private const string LightPreference = "Light";
    private const string DarkPreference = "Dark";
    private const string SystemPreference = "System";

    public static string GetSavedThemePreference()
    {
        var savedPreference = Preferences.Get(AppThemePreferenceKey, DarkPreference);
        return NormalizePreference(savedPreference);
    }

    public static void SetThemePreference(string preference)
    {
        var normalizedPreference = NormalizePreference(preference);
        Preferences.Set(AppThemePreferenceKey, normalizedPreference);
    }

    public static void ApplySavedTheme(Application? application = null)
    {
        ApplyTheme(GetSavedThemePreference(), application);
    }

    public static void ApplyTheme(string preference, Application? application = null)
    {
        var app = application ?? Application.Current;
        if (app is null)
        {
            return;
        }

        app.UserAppTheme = NormalizePreference(preference) switch
        {
            DarkPreference => AppTheme.Dark,
            SystemPreference => AppTheme.Unspecified,
            _ => AppTheme.Light
        };
    }

    public static bool IsDarkThemeActive(Application? application = null)
    {
        var app = application ?? Application.Current;
        if (app is null)
        {
            return false;
        }

        return app.UserAppTheme switch
        {
            AppTheme.Dark => true,
            AppTheme.Light => false,
            _ => app.RequestedTheme == AppTheme.Dark
        };
    }

    private static string NormalizePreference(string? preference)
    {
        return preference switch
        {
            LightPreference => LightPreference,
            DarkPreference => DarkPreference,
            SystemPreference => SystemPreference,
            _ => DarkPreference
        };
    }
}
