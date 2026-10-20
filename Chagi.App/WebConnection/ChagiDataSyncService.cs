using ITF_Taekwondo_Gradering.Logic;

namespace ITF_Taekwondo_Gradering.WebConnection;

public static class ChagiDataSyncService
{
    static readonly Lazy<ChagiDataSyncEngine> Engine = new(() => new(
        new HttpClient { Timeout = TimeSpan.FromSeconds(20) }, new Uri(ChagiVersion.BaseUrl), FileSystem.AppDataDirectory));
    static readonly ForegroundSyncLoop Foreground = new(
        token => SyncAsync(ChagiDataSyncTrigger.Automatic, token), ChagiDataSyncEngine.AutomaticInterval,
        error => AppDiagnostics.Log(error, "Automatisk datasynkning"));

    static bool _initialized;
    public static void Initialize()
    {
        if (_initialized) return;
        _initialized = true;
        try { Engine.Value.PruneOldSnapshots(); }
        catch (Exception ex) { AppDiagnostics.Log(ex, "Rensa tidigare dataversioner"); }
    }
    public static void SetForeground(bool active)
    {
        Foreground.SetForeground(active);
        if (active) MainThread.BeginInvokeOnMainThread(async () =>
        {
            try { await ResultsDataRefreshService.RefreshVisiblePagesAsync(); }
            catch (Exception ex) { AppDiagnostics.Log(ex, "Uppdatera resultat vid återgång"); }
        });
    }

    public static async Task WarmupAsync()
    {
        await SyncAsync(ChagiDataSyncTrigger.Automatic).ConfigureAwait(false);
        await CompetitorRepository.InitializeAsync().ConfigureAwait(false);
    }

    public static Task<ChagiDataSyncResult> SyncAsync(ChagiDataSyncTrigger trigger, CancellationToken cancellationToken = default)
        => Task.Run(async () =>
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, Foreground.Token);
            try
            {
                var result = await Engine.Value.SyncAsync(trigger, linked.Token).ConfigureAwait(false);
                AppDiagnostics.RecordDataSync($"{trigger}: {result.Outcome}, notModified={result.ManifestNotModified}, files={result.FilesDownloaded}, bytes={result.BytesDownloaded}");
                if (result.Outcome == ChagiDataSyncOutcome.Updated)
                {
                    // A published snapshot must also reach readers if the app backgrounds now.
                    await CompetitorRepository.ReloadFromSourceAsync().ConfigureAwait(false);
                    await MainThread.InvokeOnMainThreadAsync(() => Foreground.Token.IsCancellationRequested
                        ? Task.CompletedTask : ResultsDataRefreshService.RefreshVisiblePagesAsync()).ConfigureAwait(false);
                }
                if (trigger == ChagiDataSyncTrigger.Manual && !linked.IsCancellationRequested)
                    await ToastService.ShowAsync(result.Message).ConfigureAwait(false);
                return result;
            }
            catch (OperationCanceledException) when (linked.IsCancellationRequested)
            {
                return new ChagiDataSyncResult { Outcome = ChagiDataSyncOutcome.Skipped, AttemptedAtUtc = DateTime.UtcNow, Message = "Synkningen pausades." };
            }
        });

    public static Task<ChagiDataSyncStatus> GetStatusAsync() => Engine.Value.GetStatusAsync();
}
