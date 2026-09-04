using Backend.Data;
using Backend.Entities;
using Microsoft.EntityFrameworkCore;

namespace Backend.Features.Sites;

// Which origins may talk to this API, and where a login is allowed to land.
// Both answers come from the Sites table, so adding a blog is a POST, not a
// redeploy.
public static class SiteOrigins
{
    // ponytail: reads every site per CORS preflight. A handful of rows;
    // wrap in IMemoryCache if this ever shows up in a profile.
    public static bool IsAllowed(IServiceProvider services, string origin)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return db.Sites.AsNoTracking()
            .Select(s => s.Origins)
            .AsEnumerable()
            .SelectMany(o => o.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Contains(origin, StringComparer.OrdinalIgnoreCase);
    }

    // Anything not on the site's own origin falls back to that origin's root —
    // an unchecked returnUrl is an open redirect.
    public static string SafeReturnUrl(string? url, Site? site)
    {
        var origins = site?.OriginList() ?? [];

        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
            origins.Contains(uri.GetLeftPart(UriPartial.Authority), StringComparer.OrdinalIgnoreCase))
            return url!;

        return origins.FirstOrDefault() ?? "/";
    }
}
