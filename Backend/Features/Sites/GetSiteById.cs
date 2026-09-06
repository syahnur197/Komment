using Backend.Features.Auth;
using Backend.Services;
using FastEndpoints;

namespace Backend.Features.Sites;

public sealed class GetSiteByIdRequest
{
    public Guid Id { get; set; }
}

public sealed class GetSiteByIdEndpoint(SiteService siteService) : Endpoint<GetSiteByIdRequest, SiteResponse>
{
    private readonly SiteService _siteService = siteService;

    public override void Configure()
    {
        Get("/{id}");
        Group<SiteGroup>();
    }

    public override async Task HandleAsync(GetSiteByIdRequest req, CancellationToken ct)
    {
        var result = await _siteService.GetAsync(req.Id, UserClaims.UserIdOf(User)!.Value, ct);

        if (!result.IsOk)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(result.Value!, ct);
    }
}
