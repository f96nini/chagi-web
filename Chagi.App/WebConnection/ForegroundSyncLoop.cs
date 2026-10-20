namespace ITF_Taekwondo_Gradering.WebConnection;

public sealed class ForegroundSyncLoop(Func<CancellationToken, Task> check, TimeSpan interval,
    Action<Exception>? onError = null, Func<TimeSpan, CancellationToken, Task>? delay = null)
{
    readonly Func<TimeSpan, CancellationToken, Task> _delay = delay ?? Task.Delay;
    readonly object _gate = new();
    CancellationTokenSource? _source;
    public Task Completion { get; private set; } = Task.CompletedTask;
    public CancellationToken Token { get { lock (_gate) return _source?.Token ?? new CancellationToken(true); } }

    public void SetForeground(bool active)
    {
        lock (_gate)
        {
            if (!active)
            {
                _source?.Cancel();
                _source = null;
            }
            else if (_source == null)
            {
                var source = _source = new();
                Completion = RunAsync(source);
            }
        }
    }

    async Task RunAsync(CancellationTokenSource source)
    {
        try
        {
            await _delay(TimeSpan.FromMilliseconds(300), source.Token).ConfigureAwait(false);
            while (!source.IsCancellationRequested)
            {
                try { await check(source.Token).ConfigureAwait(false); }
                catch (OperationCanceledException) when (source.IsCancellationRequested) { break; }
                catch (Exception ex) { onError?.Invoke(ex); }
                await _delay(interval, source.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (source.IsCancellationRequested) { }
        finally
        {
            lock (_gate)
            {
                if (ReferenceEquals(_source, source)) _source = null;
                source.Dispose();
            }
        }
    }
}
