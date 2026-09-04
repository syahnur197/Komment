namespace Dashboard;

// Mirrors the API's CommentResponse. Note there is no site on it — the API
// identifies a comment's site only through the query that fetched it, so pages
// carry the site id in the route rather than reading it back off a comment.
public sealed record CommentResponse(
    Guid CommentId,
    string PostSlug,
    string Body,
    Guid? ParentCommentId,
    Guid UserId,
    string AuthorName,
    string? AuthorAvatarUrl,
    DateTime CreatedAt,
    DateTime UpdatedAt);
