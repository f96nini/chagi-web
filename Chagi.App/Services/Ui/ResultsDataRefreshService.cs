using System.Runtime.CompilerServices;
using ITF_Taekwondo_Gradering.Logic;

namespace ITF_Taekwondo_Gradering;

public interface IResultsDataPage
{
    Task InvalidateDataAsync();
    Task RefreshDataAsync();
}

internal static class ResultsDataRefreshService
{
    static readonly ConditionalWeakTable<IResultsDataPage, State> States = new();
    public static Task AppearingAsync(IResultsDataPage page)
    {
        States.GetOrCreateValue(page).Visible = true;
        return RefreshAsync(page, true);
    }
    public static void Disappearing(IResultsDataPage page) => States.GetOrCreateValue(page).Visible = false;
    public static bool IsVisible(IResultsDataPage page) => States.GetOrCreateValue(page).Visible;

    public static Task RefreshVisiblePagesAsync()
        => Shell.Current?.CurrentPage is IResultsDataPage page ? RefreshAsync(page, false) : Task.CompletedTask;

    static async Task RefreshAsync(IResultsDataPage page, bool force)
    {
        var state = States.GetOrCreateValue(page);
        if (state.Running is { } pending)
        {
            await pending;
            if (ReferenceEquals(state.Running, pending)) state.Running = null;
            await RefreshAsync(page, force);
            return;
        }
        if (!state.Visible || (!force && state.Version == CompetitorRepository.GetChangeVersion())) return;
        var running = state.Running = RunAsync(page, state);
        try { await running; }
        finally { if (ReferenceEquals(state.Running, running)) state.Running = null; }
    }

    static async Task RunAsync(IResultsDataPage page, State state)
    {
        await Task.Yield();
        try
        {
            do
            {
                var version = CompetitorRepository.GetChangeVersion();
                if (state.Version != version) await page.InvalidateDataAsync();
                if (!state.Visible) return;
                await page.RefreshDataAsync();
                state.Version = version;
            } while (state.Visible && state.Version != CompetitorRepository.GetChangeVersion());
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { AppDiagnostics.Log(ex, "Uppdatera resultat efter synkning"); }
    }

    sealed class State
    {
        public bool Visible;
        public int Version = -1;
        public Task? Running;
    }
}
