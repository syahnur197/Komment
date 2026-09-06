using FastEndpoints;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;

namespace Backend.Features.Auth;

public sealed class LoginRequest
{
    // Which blog the reader is signing in from. Omit only when bootstrapping the
    // first site — there is nowhere to redirect back to yet.
    public string? Site { get; set; }
    public string? ReturnUrl { get; set; }
}

// Kicks off the OAuth dance. The Google handler owns the token exchange and
// redirects back to /api/auth/callback once the cookie is signed in.
public sealed class LoginEndpoint(IAuthenticationSchemeProvider authenticationSchemeProvider) : Endpoint<LoginRequest>
{
    private readonly IAuthenticationSchemeProvider _authenticationSchemeProvider = authenticationSchemeProvider;

    public override void Configure()
    {
        Get("/api/auth/login");
        AllowAnonymous();
    }

    public override async Task HandleAsync(LoginRequest req, CancellationToken ct)
    {
        // The Google handler is only registered when its credentials are set.
        if (await _authenticationSchemeProvider.GetSchemeAsync(GoogleDefaults.AuthenticationScheme) is null)
        {
            await Send.ResultAsync(Results.Problem(
                "Google sign-in is not configured on this server.", statusCode: 503));
            return;
        }

        var query = $"?site={Uri.EscapeDataString(req.Site ?? "")}" +
                    $"&returnUrl={Uri.EscapeDataString(req.ReturnUrl ?? "")}";

        await Send.ResultAsync(Results.Challenge(
            new AuthenticationProperties { RedirectUri = $"/api/auth/callback{query}" },
            [GoogleDefaults.AuthenticationScheme]));
    }
}
