namespace ITF_Taekwondo_Gradering;

internal sealed class NavigationOperationCoordinator
{
    readonly SemaphoreSlim _gate = new(1, 1);
    public bool IsBusy => _gate.CurrentCount == 0;

    public async Task<bool> RunAsync(Func<Task> operation, bool wait = false)
    {
        if (wait) await _gate.WaitAsync();
        else if (!await _gate.WaitAsync(0)) return false;
        try { await operation(); return true; }
        finally { _gate.Release(); }
    }
}
