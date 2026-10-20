namespace ITF_Taekwondo_Gradering;

public static class NavigationUiService
{
    public const string HomeRoute = "//MainPage";
    static readonly NavigationOperationCoordinator Operations = new();

    public static bool IsNavigating => Operations.IsBusy;
    public static bool IsHome => Shell.Current?.CurrentPage is MainPage
        && Shell.Current.Navigation.NavigationStack.Count <= 1
        && Shell.Current.Navigation.ModalStack.Count == 0;

    public static Task GoToAsync(string route, Page? source = null)
        => RunAsync(async shell =>
        {
            if (source != null && !ReferenceEquals(shell.CurrentPage, source)) return;
            if (route == HomeRoute && IsHome) return;
            await shell.GoToAsync(route);
        }, $"Navigera till {route.Split('?')[0]}");

    public static Task GoToWithParameterAsync(string route, string parameter, object value, Page source)
        => RunAsync(async shell =>
        {
            if (!ReferenceEquals(shell.CurrentPage, source)) return;
            var key = NavigationParameterStore.Store(value);
            try { await shell.GoToAsync($"{route}?{parameter}={key}"); }
            finally { NavigationParameterStore.Take<object>(key); }
        }, $"Navigera till {route}");

    // Home requests wait for an ongoing transition; repeated taps on other links are ignored.
    public static Task GoHomeAsync() => RunAsync(async shell =>
    {
        shell.FlyoutIsPresented = false;
        while (shell.Navigation.ModalStack.Count > 0)
            await shell.Navigation.PopModalAsync(false);
        if (!IsHome) await shell.GoToAsync(HomeRoute);
    }, "Till huvudsidan", wait: true);

    public static Task GoBackOrToAsync(string fallbackRoute = HomeRoute)
        => RunAsync(async shell =>
        {
            if (shell.Navigation.ModalStack.Count > 0)
                await shell.Navigation.PopModalAsync();
            else if (shell.Navigation.NavigationStack.Count > 1)
                await shell.GoToAsync("..");
            else if (!IsHome)
                await shell.GoToAsync(fallbackRoute);
        }, "Tillbaka");

    public static void ConfigureBackButton(Shell shell)
    {
        var page = shell.CurrentPage;
        if (page == null) return;
        Shell.SetBackButtonBehavior(page, shell.Navigation.NavigationStack.Count > 1
            ? CreateBackButtonBehavior()
            : null);
    }

    public static void UseBackButton(Page page) => Shell.SetBackButtonBehavior(page, CreateBackButtonBehavior());

    static BackButtonBehavior CreateBackButtonBehavior()
        => new() { Command = new Command(async () => await GoBackOrToAsync()) };

    static Task RunAsync(Func<Shell, Task> action, string context, bool wait = false)
        => MainThread.InvokeOnMainThreadAsync(async () =>
        {
            await Operations.RunAsync(async () =>
            {
                var shell = Shell.Current;
                if (shell == null) return;
                try
                {
                    AppDiagnostics.RecordNavigation($"Start: {context}; från {shell.CurrentState?.Location}");
                    await action(shell);
                    AppDiagnostics.RecordNavigation($"Klar: {shell.CurrentState?.Location}");
                }
                catch (Exception ex)
                {
                    AppDiagnostics.Log(ex, $"{context}; från {shell.CurrentState?.Location}");
                    try { await shell.DisplayAlert("Kunde inte byta sida", "Försök igen.", "OK"); }
                    catch (Exception alertError) { AppDiagnostics.Log(alertError, "Visa navigationsfel"); }
                }
                finally
                {
                    try { ConfigureBackButton(shell); }
                    catch (Exception ex) { AppDiagnostics.Log(ex, "Uppdatera bakåtpilen"); }
                }
            }, wait);
        });
}
