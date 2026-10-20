namespace ITF_Taekwondo_Gradering;

internal sealed class PageLifetime
{
    CancellationTokenSource? _source;
    public CancellationToken Token => _source?.Token ?? new CancellationToken(true);
    public bool IsCurrent(CancellationToken token) => !token.IsCancellationRequested && token == Token;

    public void Activate()
    {
        Deactivate();
        _source?.Dispose();
        _source = new CancellationTokenSource();
    }

    public void Deactivate() => _source?.Cancel();
}
