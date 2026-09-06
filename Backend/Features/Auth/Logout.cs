using FastEndpoints;
using Microsoft.AspNetCore.Authentication;

namespace Backend.Features.Auth;

public sealed class LogoutEndpoint : EndpointWithoutRequest
{
    public override void Configure()
    {
        Post("/api/auth/logout");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        await HttpContext.SignOutAsync(Backend.Features.Auth.AuthSchemes.Reader);
        await Send.NoContentAsync(ct);
    }
}
