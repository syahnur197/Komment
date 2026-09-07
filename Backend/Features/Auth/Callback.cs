using System.Security.Claims;
using Backend.Features.Sites;
using Backend.Services;
using FastEndpoints;
using Microsoft.AspNetCore.Authentication;

namespace Backend.Features.Auth;

public sealed class CallbackRequest
{
    public string? ReturnUrl { get; set; }
}

// Google has already signed the cookie in by the time we get here. This is where
// the account becomes a row: upsert the user, then re-issue the cookie carrying
// our own user id so no endpoint has to look the user up again.
public sealed class CallbackEndpoint(AccountService accountService, AllowedOrigins allowedOrigins) : Endpoint<CallbackRequest>
{
    private readonly AccountService _accountService = accountService;
    private readonly AllowedOrigins _allowedOrigins = allowedOrigins;

    public override void Configure()
    {
        Get("/api/auth/callback");
    }

    public override async Task HandleAsync(CallbackRequest req, CancellationToken ct)
    {
        var googleId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = User.FindFirstValue(ClaimTypes.Email);

        if (googleId is null || email is null)
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        var user = await _accountService.UpsertGoogleAsync(
            googleId, email, User.FindFirstValue(ClaimTypes.Name), User.FindFirstValue("picture"), ct);

        // Keep the Google identity (it carries the picture claim) and add ours.
        var identity = (ClaimsIdentity)User.Identity!;
        identity.AddClaim(new Claim(UserClaims.UserId, user.UserId.ToString()));

        if (user.IsSiteAdmin)
            identity.AddClaim(new Claim(ClaimTypes.Role, UserClaims.SiteAdminRole));

        await HttpContext.SignInAsync(Backend.Features.Auth.AuthSchemes.Reader, new ClaimsPrincipal(identity));

        await Send.ResultAsync(Results.Redirect(_allowedOrigins.SafeReturnUrl(req.ReturnUrl)));
    }
}
