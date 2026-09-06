using Backend.Features.Auth;
using Backend.Services;
using FastEndpoints;

namespace Backend.Features.Sites;

public sealed class DeleteSiteRequest
{
    public Guid Id { get; set; }
}

// Takes every comment on that site with it (required FK, so EF cascades).
public sealed class DeleteSiteEndpoint : Endpoint<DeleteSiteRequest>
{
    public SiteService Sites { get; set; } = default!;

    public override void Configure()
    {
        Delete("/{id}");
        Group<SiteGroup>();
    }

    public override async Task HandleAsync(DeleteSiteRequest req, CancellationToken ct)
    {
        var result = await Sites.DeleteAsync(req.Id, UserClaims.UserIdOf(User)!.Value, ct);

        if (!result.IsOk)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.NoContentAsync(ct);
    }
}
