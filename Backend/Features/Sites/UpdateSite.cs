using Backend.Features.Auth;
using Backend.Services;
using FastEndpoints;
using FluentValidation;

namespace Backend.Features.Sites;

// Slug is not editable: the blogs already embed it in their requests.
public sealed class UpdateSiteRequest
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public string? Origins { get; set; }
}

public sealed class UpdateSiteValidator : Validator<UpdateSiteRequest>
{
    public UpdateSiteValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200).When(x => x.Name is not null);
        RuleFor(x => x.Origins).NotEmpty().MaximumLength(1000).When(x => x.Origins is not null);
    }
}

public sealed class UpdateSiteEndpoint : Endpoint<UpdateSiteRequest, SiteResponse>
{
    public SiteService Sites { get; set; } = default!;

    public override void Configure()
    {
        Patch("/{id}");
        Group<SiteGroup>();
    }

    public override async Task HandleAsync(UpdateSiteRequest req, CancellationToken ct)
    {
        var result = await Sites.UpdateAsync(
            req.Id, UserClaims.UserIdOf(User)!.Value, req.Name, req.Origins, ct);

        if (!result.IsOk)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(result.Value!, ct);
    }
}
