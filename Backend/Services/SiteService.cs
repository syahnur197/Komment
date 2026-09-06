using Backend.Data;
using Backend.Entities;
using Backend.Features.Sites;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

// Registering and moderating blogs. Every method that touches one site takes the
// owner's id and filters on it — being a site admin does not mean seeing someone
// else's blog, and making that a parameter rather than a convention means a
// caller cannot forget it.
public sealed class SiteService(AppDbContext db)
{
    public async Task<List<SiteResponse>> ListAsync(Guid ownerId, CancellationToken ct)
    {
        var sites = await db.Sites.AsNoTracking()
            .Where(s => s.OwnerUserId == ownerId)
            .OrderBy(s => s.Slug)
            .ToListAsync(ct);

        return sites.Select(SiteResponse.From).ToList();
    }

    public async Task<Result<SiteResponse>> GetAsync(Guid siteId, Guid ownerId, CancellationToken ct)
    {
        var site = await db.Sites.AsNoTracking()
            .FirstOrDefaultAsync(s => s.SiteId == siteId && s.OwnerUserId == ownerId, ct);

        // Someone else's site is reported as missing, not forbidden: whether a
        // given id exists is not a stranger's business.
        return site is null
            ? Result<SiteResponse>.NotFound()
            : Result<SiteResponse>.Ok(SiteResponse.From(site));
    }

    public async Task<Result<SiteResponse>> CreateAsync(
        Guid ownerId, string slug, string name, string origins, CancellationToken ct)
    {
        if (await db.Sites.AnyAsync(s => s.Slug == slug, ct))
            return Result<SiteResponse>.Invalid(nameof(Site.Slug), "That slug is taken.");

        var site = new Site { Slug = slug, Name = name, Origins = origins, OwnerUserId = ownerId };

        db.Sites.Add(site);
        await db.SaveChangesAsync(ct);

        return Result<SiteResponse>.Ok(SiteResponse.From(site));
    }

    // Slug is deliberately not updatable: blogs already embed it in their requests.
    public async Task<Result<SiteResponse>> UpdateAsync(
        Guid siteId, Guid ownerId, string? name, string? origins, CancellationToken ct)
    {
        var site = await db.Sites.FirstOrDefaultAsync(s => s.SiteId == siteId && s.OwnerUserId == ownerId, ct);

        if (site is null) return Result<SiteResponse>.NotFound();

        site.Name = name ?? site.Name;
        site.Origins = origins ?? site.Origins;

        await db.SaveChangesAsync(ct);

        return Result<SiteResponse>.Ok(SiteResponse.From(site));
    }

    // Not owner-scoped, and deliberately so: the OAuth callback needs a site's
    // origins to bound its redirect, and the reader signing in does not own it.
    public Task<Site?> FindBySlugAsync(string? slug, CancellationToken ct) =>
        db.Sites.AsNoTracking().FirstOrDefaultAsync(s => s.Slug == slug, ct);

    // Takes every comment on that site with it (required FK, so EF cascades).
    public async Task<Result> DeleteAsync(Guid siteId, Guid ownerId, CancellationToken ct)
    {
        var site = await db.Sites.FirstOrDefaultAsync(s => s.SiteId == siteId && s.OwnerUserId == ownerId, ct);

        if (site is null) return Result.NotFound();

        db.Sites.Remove(site);
        await db.SaveChangesAsync(ct);

        return Result.Ok();
    }
}
