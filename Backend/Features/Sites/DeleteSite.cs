using Backend.Data;
using Backend.Features.Auth;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;

namespace Backend.Features.Sites;

public sealed class DeleteSiteRequest
{
    public Guid Id { get; set; }
}

// Takes every comment on that site with it (required FK, so EF cascades).
public sealed class DeleteSiteEndpoint : Endpoint<DeleteSiteRequest>
{
    public AppDbContext Db { get; set; } = default!;

    public override void Configure()
    {
        Delete("/{id}");
        Group<SiteGroup>();
    }

    public override async Task HandleAsync(DeleteSiteRequest req, CancellationToken ct)
    {
        var userId = UserClaims.UserIdOf(User)!.Value;

        var site = await Db.Sites.FirstOrDefaultAsync(s => s.SiteId == req.Id && s.OwnerUserId == userId, ct);

        if (site is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        Db.Sites.Remove(site);
        await Db.SaveChangesAsync(ct);

        await Send.NoContentAsync(ct);
    }
}
