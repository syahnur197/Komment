using System.ComponentModel.DataAnnotations;

namespace Backend.Entities;

// Two kinds of account share this table: readers who signed in with Google
// (GoogleId set, no password) and site admins who registered with a username
// and password. Nothing stops one person being both — they are separate rows.
public class User : ITimestamped
{
    public Guid UserId { get; set; }

    // Google's "sub" claim — stable per account, unlike email. Null for
    // username/password accounts.
    [MaxLength(64)]
    public string? GoogleId { get; set; }

    [MaxLength(100)]
    public string? Username { get; set; }

    // PasswordHasher output (PBKDF2, salt and iteration count embedded).
    [MaxLength(400)]
    public string? PasswordHash { get; set; }

    // Can register sites and moderate their comments.
    public bool IsSiteAdmin { get; set; }

    [MaxLength(320)]
    public required string Email { get; set; }

    [MaxLength(200)]
    public required string Name { get; set; }

    [MaxLength(500)]
    public string? AvatarUrl { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
