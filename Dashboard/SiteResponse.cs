namespace Dashboard;

// Mirrors the API's SiteResponse. Origins is an array on the way out but a
// comma-separated string on the way in — see SiteEditor.
public sealed record SiteResponse(
    Guid SiteId,
    string Slug,
    string Name,
    string[] Origins,
    DateTime CreatedAt,
    DateTime UpdatedAt);
