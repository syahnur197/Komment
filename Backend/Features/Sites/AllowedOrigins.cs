namespace Backend.Features.Sites;

// Which origins may talk to this API, and where a login is allowed to land.
// One admin per installation, so this is configuration rather than data: it
// comes from ALLOWED_ORIGINS in .env and never from the database.
//
// The trade against the Sites table it replaced: adding a blog is now a restart,
// not a POST. In exchange every check here is an array scan against a list read
// once at startup, instead of a query per CORS preflight.
public sealed class AllowedOrigins(IConfiguration cfg)
{
    public string[] All { get; } =
        (cfg["ALLOWED_ORIGINS"] ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public bool Contains(string? origin) =>
        origin is not null && All.Contains(origin, StringComparer.OrdinalIgnoreCase);

    // An unchecked returnUrl is an open redirect, so anything off the list falls
    // back to the first configured origin.
    public string SafeReturnUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && Contains(uri.GetLeftPart(UriPartial.Authority))
            ? url!
            : All.FirstOrDefault() ?? "/";
}
