using Backend.Entities;
using FastEndpoints;

namespace Backend.Features.Comments;

// The group is the shared prefix + shared configuration, like MapGroup() or
// [Route("api/[controller]")]. Endpoints opt in with Group<CommentGroup>().
// Reads are opened up per-endpoint; writes stay behind the cookie.
public sealed class CommentGroup : Group
{
    public CommentGroup()
    {
        Configure("/api/comment", _ => { });
    }
}

public sealed record CommentResponse(
    Guid CommentId,
    string PostSlug,
    string? PostUrl,
    string Body,
    Guid? ParentCommentId,
    Guid UserId,
    string AuthorName,
    string? AuthorAvatarUrl,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    public static CommentResponse From(Comment c) =>
        new(c.CommentId, c.PostSlug, c.PostUrl, c.Body, c.ParentCommentId, c.UserId, c.User.Name, c.User.AvatarUrl, c.CreatedAt, c.UpdatedAt);
}
