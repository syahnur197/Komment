using Backend.Services;
using FastEndpoints;
using FluentValidation;
using Microsoft.IdentityModel.Tokens;

namespace Backend.Features.Auth;

public sealed class IssueTokenRequest
{
    public string Username { get; set; } = default!;
    public string Password { get; set; } = default!;
}

public sealed class IssueTokenValidator : Validator<IssueTokenRequest>
{
    public IssueTokenValidator()
    {
        RuleFor(x => x.Username).NotEmpty();
        RuleFor(x => x.Password).NotEmpty();
    }
}

// Everything the caller needs to store, in one response: readers get their
// identity from /api/auth/me off the cookie, but a token client has no cookie to
// ask about, and a second round trip on every sign-in buys nothing.
public sealed record IssueTokenResponse(
    string AccessToken,
    DateTime ExpiresAt,
    Guid UserId,
    string Email,
    string Name,
    bool IsSiteAdmin);

// The admin sign-in. Readers use Google and get a cookie; this hands back a
// bearer token for any API client to carry.
public sealed class IssueTokenEndpoint : Endpoint<IssueTokenRequest, IssueTokenResponse>
{
    public AccountService Accounts { get; set; } = default!;
    public SymmetricSecurityKey SigningKey { get; set; } = default!;

    public override void Configure()
    {
        Post("/api/auth/token");
        AllowAnonymous();
    }

    public override async Task HandleAsync(IssueTokenRequest req, CancellationToken ct)
    {
        var user = await Accounts.VerifyPasswordAsync(req.Username, req.Password, ct);

        // One message for "no such user" and "wrong password" — the difference
        // is a free account-enumeration oracle.
        if (user is null)
        {
            await Send.ResultAsync(Results.Problem("Invalid username or password.", statusCode: 401));
            return;
        }

        var (token, expiresAt) = Tokens.Create(user, SigningKey);

        await Send.OkAsync(new IssueTokenResponse(
            token, expiresAt, user.UserId, user.Email, user.Name, user.IsSiteAdmin), ct);
    }
}
