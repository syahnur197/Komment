using Backend.Features.Auth;
using Backend.Services;
using FastEndpoints;
using FluentValidation;
using FluentValidation.Results;

namespace Backend.Features.Sites;

public sealed class CreateSiteRequest
{
    public string Slug { get; set; } = default!;
    public string Name { get; set; } = default!;
}

public sealed class CreateSiteValidator : Validator<CreateSiteRequest>
{
    public CreateSiteValidator()
    {
        RuleFor(x => x.Slug)
            .NotEmpty().MaximumLength(100)
            .Matches("^[a-z0-9-]+$").WithMessage("Lowercase letters, digits and dashes only.");

        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
    }
}

public sealed class CreateSiteEndpoint(SiteService siteService) : Endpoint<CreateSiteRequest, SiteResponse>
{
    private readonly SiteService _siteService = siteService;

    public override void Configure()
    {
        Post("");
        Group<SiteGroup>();
    }

    public override async Task HandleAsync(CreateSiteRequest req, CancellationToken ct)
    {
        var result = await _siteService.CreateAsync(
            UserClaims.UserIdOf(User)!.Value, req.Slug, req.Name, ct);

        if (!result.IsOk)
        {
            ValidationFailures.Add(new ValidationFailure(result.Field!, result.Message!));
            await Send.ErrorsAsync(cancellation: ct);
            return;
        }

        await Send.CreatedAtAsync<GetSiteByIdEndpoint>(
            new { Id = result.Value!.SiteId }, result.Value, cancellation: ct);
    }
}
