using Backend.Entities;
using Backend.Features.Auth;
using FastEndpoints;

namespace Backend.Features.Sites;

// Admin surface: registering the blogs that may use this comment service.
// Site admins only, and every endpoint is additionally scoped to the sites the
// caller owns — being an admin does not mean seeing someone else's blog.
public sealed class SiteGroup : Group
{
    public SiteGroup()
    {
        Configure("/api/site", ep => ep.Roles(UserClaims.SiteAdminRole));
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
