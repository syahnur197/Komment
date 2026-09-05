using System.ComponentModel.DataAnnotations;

namespace Backend.Entities;

public class Comment : ITimestamped
{
    public Guid CommentId { get; set; }

    public Guid SiteId { get; set; }
    public Site Site { get; set; } = default!;

    // Page identity — whatever the blog sends as the permalink key.
    [MaxLength(300)]
    public required string PostSlug { get; set; }

    [MaxLength(4000)]
    public required string Body { get; set; }

    // Self-reference: a reply is just a comment with a parent. Null = top level.
    public Guid? ParentCommentId { get; set; }
    public Comment? Parent { get; set; }
    public ICollection<Comment> Replies { get; set; } = [];

    public Guid UserId { get; set; }
    public User User { get; set; } = default!;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
