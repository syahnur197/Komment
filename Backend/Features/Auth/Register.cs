using Backend.Services;
using FastEndpoints;
using FluentValidation;
using FluentValidation.Results;

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

// Creates a site admin. Whether this is open to anyone is AccountService's call
// (MULTI_TENANCY); this only turns the answer into a status code.
public sealed class RegisterEndpoint : Endpoint<RegisterRequest>
{
    public AccountService Accounts { get; set; } = default!;

    public override void Configure()
    {
        Post("/api/auth/register");
        AllowAnonymous();
    }

    public override async Task HandleAsync(RegisterRequest req, CancellationToken ct)
    {
        var result = await Accounts.RegisterAsync(req.Username, req.Email, req.Name, req.Password, ct);

        switch (result.Kind)
        {
            case ResultKind.Forbidden:
                await Send.ResultAsync(Results.Problem(
                    "This instance is single-tenant and already has an admin.", statusCode: 403));
                return;

            case ResultKind.Invalid:
                ValidationFailures.Add(new ValidationFailure(result.Field!, result.Message!));
                await Send.ErrorsAsync(cancellation: ct);
                return;
        }

        await Send.NoContentAsync(ct);
    }
}
