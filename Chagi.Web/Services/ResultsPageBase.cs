using Chagi.Core.Data;
using Microsoft.AspNetCore.Components;

namespace Chagi.Web.Services;

public abstract class ResultsPageBase : ComponentBase, IDisposable
{
    [Inject] protected PublishedResultsClient Source { get; set; } = null!;
    protected ResultsData? Data { get; private set; }
    protected bool Busy { get; private set; } = true;
    protected string? Error { get; private set; }
    protected virtual bool LatestOnly => false;
    readonly CancellationTokenSource lifetime = new();

    protected override async Task OnInitializedAsync() => await LoadAsync();
    protected async Task RefreshAsync() => await LoadAsync(true);
    async Task LoadAsync(bool refresh = false)
    {
        Busy = true;
        Error = null;
        try { Data = await Source.LoadAsync(LatestOnly, refresh, lifetime.Token); }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch (Exception) { Error = "Resultaten kunde inte hämtas. Kontrollera anslutningen och försök igen."; }
        finally { Busy = false; }
    }
    public void Dispose() { lifetime.Cancel(); lifetime.Dispose(); GC.SuppressFinalize(this); }
}
