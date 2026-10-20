using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace Chagi.Web.Services;

public sealed class BrowserRevalidationHandler : DelegatingHandler
{
    public BrowserRevalidationHandler() : base(new HttpClientHandler()) { }
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        request.SetBrowserRequestCache(BrowserRequestCache.NoCache);
        return base.SendAsync(request, token);
    }
}
