using System.Security.Claims;

namespace Dashboard;

// The API authenticates with a cookie, and Blazor Server — not the browser — is
// what talks to it. Login stashes the API's Set-Cookie value as a claim on the
// Dashboard's own auth cookie; this puts it back on the wire per request, so
// each signed-in user's calls carry that user's API session and no one else's.
public sealed class BackendSessionHandler(IHttpContextAccessor accessor) : DelegatingHandler
{
    public const string ClientName = "backend";
    public const string SessionClaim = "backend-session";

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (accessor.HttpContext?.User.FindFirstValue(SessionClaim) is { Length: > 0 } cookie)
            request.Headers.Add("Cookie", cookie);

        return base.SendAsync(request, ct);
    }
}
