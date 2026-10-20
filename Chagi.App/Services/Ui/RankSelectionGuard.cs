using Microsoft.Maui.Storage;

namespace ITF_Taekwondo_Gradering;

internal static class RankSelectionGuard
{
    internal static async Task<bool> EnsureSelectedAsync(ContentPage page)
    {
        ArgumentNullException.ThrowIfNull(page);

        var selectedRank = Preferences.Get("RankSelection", string.Empty);
        if (!string.IsNullOrWhiteSpace(selectedRank))
            return true;

        var openSettings = await page.DisplayAlert(
            "Grad saknas",
            "Välj en grad i Inställningar innan du använder Studera eller Quiz.",
            "Öppna inställningar",
            "Tillbaka");

        await NavigationUiService.GoToAsync(openSettings ? "///settings" : "///MainPage");
        return false;
    }
}
