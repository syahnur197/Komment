using Backend.Data;
using Backend.Features.Auth;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;

namespace Backend.Features.Sites;

// Your sites, not everyone's — the list doubles as the CORS allowlist, so it
// stays private.
public sealed class GetAllSitesEndpoint : EndpointWithoutRequest<List<SiteResponse>>
{
    public AppDbContext Db { get; set; } = default!;

    public override void Configure()
    {
        Get("");
        Group<SiteGroup>();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = UserClaims.UserIdOf(User)!.Value;

        var sites = await Db.Sites.AsNoTracking()
            .Where(s => s.OwnerUserId == userId)
            .OrderBy(s => s.Slug)
            .ToListAsync(ct);

        await Send.OkAsync(sites.Select(SiteResponse.From).ToList(), ct);
    }
}
