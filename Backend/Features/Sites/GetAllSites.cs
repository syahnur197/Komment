using Backend.Features.Auth;
using Backend.Services;
using FastEndpoints;

namespace Backend.Features.Sites;

// Your sites, not everyone's — the list doubles as the CORS allowlist, so it
// stays private.
public sealed class GetAllSitesEndpoint : EndpointWithoutRequest<List<SiteResponse>>
{
    public SiteService Sites { get; set; } = default!;

    public override void Configure()
    {
        Get("");
        Group<SiteGroup>();
    }

    public override async Task HandleAsync(CancellationToken ct) =>
        await Send.OkAsync(await Sites.ListAsync(UserClaims.UserIdOf(User)!.Value, ct), ct);
}
