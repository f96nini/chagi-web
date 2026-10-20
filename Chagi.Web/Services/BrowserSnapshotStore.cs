using Chagi.Core.Data;
using Microsoft.JSInterop;

namespace Chagi.Web.Services;

public sealed class BrowserSnapshotStore(IJSRuntime js) : IDataSnapshotStore
{
    public Task<PublishedSnapshot?> ReadAsync(CancellationToken token = default) =>
        js.InvokeAsync<PublishedSnapshot?>("chagiCache.read", token).AsTask();
    public Task WriteAsync(PublishedSnapshot snapshot, CancellationToken token = default) =>
        js.InvokeVoidAsync("chagiCache.write", token, snapshot).AsTask();
}
