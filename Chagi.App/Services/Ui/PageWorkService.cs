using System.Runtime.CompilerServices;

namespace ITF_Taekwondo_Gradering;

/// <summary>Page-local progress, without modal navigation or OnAppearing re-entry.</summary>
public static class PageWorkService
{
    static readonly ConditionalWeakTable<ContentPage, BusyState> States = new();

    // The workflow resumes on the UI thread. Put computation in RunBackgroundAsync
    // or Task.Run; never create or mutate MAUI controls on a worker thread.
    public static Task RunAsync(ContentPage page, Func<Task> workflow, string status = "Laddar...",
        CancellationToken cancellationToken = default)
        => MainThread.InvokeOnMainThreadAsync(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var state = States.GetValue(page, CreateState);
            state.Requests++;
            state.Label.Text = status;
            state.Overlay.IsVisible = true;
            state.Spinner.IsRunning = true;
            try
            {
                // Allow a frame before a cached operation or UI binding completes synchronously.
                await Task.Delay(32, cancellationToken);
                await workflow();
            }
            finally
            {
                if (--state.Requests == 0 && state.IsActive)
                {
                    state.Spinner.IsRunning = false;
                    state.Overlay.IsVisible = false;
                }
            }
        });

    public static async Task<T> RunBackgroundAsync<T>(ContentPage page, Func<Task<T>> work,
        string status = "Laddar...", CancellationToken cancellationToken = default)
    {
        T result = default!;
        await RunAsync(page, async () => result = await Task.Run(work, cancellationToken).WaitAsync(cancellationToken),
            status, cancellationToken);
        return result;
    }

    static BusyState CreateState(ContentPage page)
    {
        var content = page.Content;
        page.Content = null;
        var spinner = new ActivityIndicator
        {
            WidthRequest = 40, HeightRequest = 40,
            HorizontalOptions = LayoutOptions.Center, Color = Colors.White
        };
        var label = new Label { TextColor = Colors.White, HorizontalTextAlignment = TextAlignment.Center };
        var overlay = new Grid
        {
            IsVisible = false, ZIndex = 1000,
            BackgroundColor = Color.FromRgba(12, 20, 28, 220),
            Children = { new VerticalStackLayout
            {
                VerticalOptions = LayoutOptions.Center, HorizontalOptions = LayoutOptions.Center,
                Spacing = 12, Padding = 24, Children = { spinner, label }
            } }
        };
        var root = new Grid();
        if (content != null) root.Add(content);
        root.Add(overlay);
        page.Content = root;
        var state = new BusyState(overlay, spinner, label);
        page.Appearing += (_, _) => state.IsActive = true;
        page.Disappearing += (_, _) =>
        {
            state.IsActive = false;
            spinner.IsRunning = false;
            overlay.IsVisible = false;
        };
        return state;
    }

    sealed class BusyState(Grid overlay, ActivityIndicator spinner, Label label)
    {
        public Grid Overlay { get; } = overlay;
        public ActivityIndicator Spinner { get; } = spinner;
        public Label Label { get; } = label;
        public int Requests { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
