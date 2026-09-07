using System.ComponentModel.DataAnnotations;

namespace Backend.Entities;

// A tenant: one blog. Everything else hangs off this.
public sealed class Site : ITimestamped
{
    public Guid SiteId { get; set; }

    // What the frontend sends to identify itself, e.g. "syahnur-blog".
    [MaxLength(100)]
    public required string Slug { get; set; }

    [MaxLength(200)]
    public required string Name { get; set; }

    public Guid OwnerUserId { get; set; }
    public User Owner { get; set; } = default!;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
