using Backend.Entities;
using Backend.Features.Auth;
using FastEndpoints;

namespace Backend.Features.Sites;

// Admin surface: registering the blogs that may use this comment service.
// Site admins only, and every endpoint is additionally scoped to the sites the
// caller owns — being an admin does not mean seeing someone else's blog.
//
// Named scheme rather than the by-path default: everything else under /api is a
// blog carrying the reader cookie, and an admin has the console's cookie instead.
public sealed class SiteGroup : Group
{
    public SiteGroup()
    {
        Configure("/api/site", ep =>
        {
            ep.AuthSchemes(AuthSchemes.Admin);
            ep.Roles(UserClaims.SiteAdminRole);
        });
    }
}

public sealed record SiteResponse(
    Guid SiteId,
    string Slug,
    string Name,
    string[] Origins,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    public static SiteResponse From(Site s) =>
        new(s.SiteId, s.Slug, s.Name, s.OriginList(), s.CreatedAt, s.UpdatedAt);
}
