using System.ComponentModel.DataAnnotations;

namespace Backend.Entities;

// A tenant: one blog. Everything else hangs off this.
public class Site : ITimestamped
{
    public Guid SiteId { get; set; }

    // What the frontend sends to identify itself, e.g. "syahnur-blog".
    [MaxLength(100)]
    public required string Slug { get; set; }

    [MaxLength(200)]
    public required string Name { get; set; }

    // ponytail: comma-separated origins in one column — a blog needs its prod
    // host plus a localhost for drafting, and that is the whole requirement.
    // Split into its own table if origins ever need their own attributes.
    [MaxLength(1000)]
    public required string Origins { get; set; }

    public Guid OwnerUserId { get; set; }
    public User Owner { get; set; } = default!;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public string[] OriginList() =>
        Origins.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
