using Backend.Data;
using Backend.Features.Auth;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;

namespace Backend.Features.Sites;

public sealed class GetSiteByIdRequest
{
    public Guid Id { get; set; }
}

public sealed class GetSiteByIdEndpoint : Endpoint<GetSiteByIdRequest, SiteResponse>
{
    public AppDbContext Db { get; set; } = default!;

    public override void Configure()
    {
        Get("/{id}");
        Group<SiteGroup>();
    }

    public override async Task HandleAsync(GetSiteByIdRequest req, CancellationToken ct)
    {
        var userId = UserClaims.UserIdOf(User)!.Value;

        var site = await Db.Sites.AsNoTracking()
            .FirstOrDefaultAsync(s => s.SiteId == req.Id && s.OwnerUserId == userId, ct);

        if (site is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(SiteResponse.From(site), ct);
    }
}
