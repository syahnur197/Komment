using System.Security.Claims;
using Backend.Data;
using Backend.Entities;
using FastEndpoints;
using FluentValidation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Backend.Features.Auth;

public sealed class PasswordLoginRequest
{
    public string Username { get; set; } = default!;
    public string Password { get; set; } = default!;
}

public sealed class PasswordLoginValidator : Validator<PasswordLoginRequest>
{
    public PasswordLoginValidator()
    {
        RuleFor(x => x.Username).NotEmpty();
        RuleFor(x => x.Password).NotEmpty();
    }
}

// The admin sign-in. Readers use Google; this is for managing sites and
// moderating comments. Same cookie either way.
public sealed class PasswordLoginEndpoint : Endpoint<PasswordLoginRequest>
{
    public AppDbContext Db { get; set; } = default!;

    public override void Configure()
    {
        Post("/api/auth/login/password");
        AllowAnonymous();
    }

    public override async Task HandleAsync(PasswordLoginRequest req, CancellationToken ct)
    {
        var user = await Db.Users.FirstOrDefaultAsync(u => u.Username == req.Username, ct);

        // One message for "no such user" and "wrong password" — the difference
        // is a free account-enumeration oracle.
        var verified = user?.PasswordHash is not null &&
                       new PasswordHasher<User>().VerifyHashedPassword(user, user.PasswordHash, req.Password)
                           is not PasswordVerificationResult.Failed;

        if (!verified)
        {
            await Send.ResultAsync(Results.Problem("Invalid username or password.", statusCode: 401));
            return;
        }

        var identity = new ClaimsIdentity(UserClaims.For(user!), CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

        await Send.NoContentAsync(ct);
    }
}
