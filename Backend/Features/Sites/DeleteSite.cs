using Backend.Features.Auth;
using Backend.Services;
using FastEndpoints;

namespace Backend.Features.Sites;

public sealed class DeleteSiteRequest
{
    public Guid Id { get; set; }
}

// Takes every comment on that site with it (required FK, so EF cascades).
public sealed class DeleteSiteEndpoint(SiteService siteService) : Endpoint<DeleteSiteRequest>
{
    private readonly SiteService _siteService = siteService;

    public override void Configure()
    {
        Delete("/{id}");
        Group<SiteGroup>();
    }

    public override async Task HandleAsync(DeleteSiteRequest req, CancellationToken ct)
    {
        var result = await _siteService.DeleteAsync(req.Id, UserClaims.UserIdOf(User)!.Value, ct);

        if (!result.IsOk)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.NoContentAsync(ct);
    }
}
