using Backend.Data;
using Backend.Entities;
using FastEndpoints;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Backend.Features.Auth;

public sealed class RegisterRequest
{
    public string Username { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string Password { get; set; } = default!;
}

public sealed class RegisterValidator : Validator<RegisterRequest>
{
    public RegisterValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty().MaximumLength(100)
            .Matches("^[a-zA-Z0-9._-]+$").WithMessage("Letters, digits, dot, underscore and dash only.");

        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8).MaximumLength(200);
    }
}

// Creates a site admin. Whether this is open to anyone depends on the mode:
//   MULTI_TENANCY=true  — SaaS: anyone may sign up and register their own sites.
//   MULTI_TENANCY=false — self-hosted: the first registration takes the box and
//                         every one after it is refused.
public sealed class RegisterEndpoint : Endpoint<RegisterRequest>
{
    public AppDbContext Db { get; set; } = default!;
    public IConfiguration Cfg { get; set; } = default!;

    public override void Configure()
    {
        Post("/api/auth/register");
        AllowAnonymous();
    }

    public override async Task HandleAsync(RegisterRequest req, CancellationToken ct)
    {
        var multiTenant = Cfg.GetValue("MULTI_TENANCY", false);

        if (!multiTenant && await Db.Users.AnyAsync(u => u.IsSiteAdmin, ct))
        {
            await Send.ResultAsync(Results.Problem(
                "This instance is single-tenant and already has an admin.", statusCode: 403));
            return;
        }

        if (await Db.Users.AnyAsync(u => u.Username == req.Username, ct))
        {
            AddError(r => r.Username, "That username is taken.");
            await Send.ErrorsAsync(cancellation: ct);
            return;
        }

        var user = new User
        {
            Username = req.Username,
            Email = req.Email,
            Name = req.Name,
            IsSiteAdmin = true,
        };

        user.PasswordHash = new PasswordHasher<User>().HashPassword(user, req.Password);

        Db.Users.Add(user);
        await Db.SaveChangesAsync(ct);

        await Send.NoContentAsync(ct);
    }
}
