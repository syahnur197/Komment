using Backend.Data;
using Backend.Features.Auth;
using FastEndpoints;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

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
    public AppDbContext Db { get; set; } = default!;

    public override void Configure()
    {
        Patch("/{id}");
        Group<SiteGroup>();
    }

    public override async Task HandleAsync(UpdateSiteRequest req, CancellationToken ct)
    {
        var userId = UserClaims.UserIdOf(User)!.Value;

        var site = await Db.Sites.FirstOrDefaultAsync(s => s.SiteId == req.Id && s.OwnerUserId == userId, ct);

        if (site is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        site.Name = req.Name ?? site.Name;
        site.Origins = req.Origins ?? site.Origins;

        await Db.SaveChangesAsync(ct);

        await Send.OkAsync(SiteResponse.From(site), ct);
    }
}
