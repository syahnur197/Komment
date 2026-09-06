using Backend.Data;
using Backend.Entities;
using Backend.Features.Comments;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

// Reading and moderating comments. Reads are open — a blog renders them to
// anonymous visitors — so those take no user id; every write takes the acting
// user and decides for itself whether that user may.
public sealed class CommentService(AppDbContext db)
{
    // Flat and oldest-first: replies carry ParentCommentId and the caller nests
    // them. Cheaper than shipping a tree builder and a depth limit.
    public async Task<List<CommentResponse>> ListAsync(string siteSlug, string? postSlug, CancellationToken ct)
    {
        var comments = await db.Comments.AsNoTracking()
            .Where(c => c.Site.Slug == siteSlug)
            .Where(c => postSlug == null || c.PostSlug == postSlug)
            .OrderBy(c => c.CreatedAt)
            .Include(c => c.User)
            .ToListAsync(ct);

        return comments.Select(CommentResponse.From).ToList();
    }

    public async Task<Result<CommentResponse>> GetAsync(Guid commentId, CancellationToken ct)
    {
        var comment = await db.Comments.AsNoTracking()
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.CommentId == commentId, ct);

        return comment is null
            ? Result<CommentResponse>.NotFound()
            : Result<CommentResponse>.Ok(CommentResponse.From(comment));
    }

    public async Task<Result<CommentResponse>> CreateAsync(
        Guid userId, string siteSlug, string postSlug, string body, Guid? parentCommentId, CancellationToken ct)
    {
        var siteId = await db.Sites
            .Where(s => s.Slug == siteSlug)
            .Select(s => s.SiteId)
            .FirstOrDefaultAsync(ct);

        if (siteId == Guid.Empty)
            return Result<CommentResponse>.Invalid("site", "No such site.");

        if (parentCommentId is { } parentId)
        {
            // A reply has to hang off a real comment on the same post of the same
            // site, or the thread the blog renders makes no sense.
            var parentExists = await db.Comments.AnyAsync(
                c => c.CommentId == parentId && c.SiteId == siteId && c.PostSlug == postSlug, ct);

            if (!parentExists)
                return Result<CommentResponse>.Invalid(nameof(Comment.ParentCommentId), "No such comment on this post.");
        }

        var comment = new Comment
        {
            SiteId = siteId,
            PostSlug = postSlug,
            Body = body,
            ParentCommentId = parentCommentId,
            UserId = userId,
        };

        db.Comments.Add(comment);
        await db.SaveChangesAsync(ct);
        await db.Entry(comment).Reference(c => c.User).LoadAsync(ct);

        return Result<CommentResponse>.Ok(CommentResponse.From(comment));
    }

    public async Task<Result<CommentResponse>> UpdateAsync(
        Guid commentId, Guid userId, string body, CancellationToken ct)
    {
        var comment = await db.Comments.Include(c => c.User)
            .FirstOrDefaultAsync(c => c.CommentId == commentId, ct);

        if (comment is null) return Result<CommentResponse>.NotFound();

        // Not business logic — a trust boundary. Anyone signed in could otherwise
        // rewrite anyone else's comment. Editing is author-only even for admins;
        // moderation is deletion, not impersonation.
        if (comment.UserId != userId) return Result<CommentResponse>.Forbidden();

        comment.Body = body;
        await db.SaveChangesAsync(ct);

        return Result<CommentResponse>.Ok(CommentResponse.From(comment));
    }

    public async Task<Result> DeleteAsync(Guid commentId, Guid userId, bool isSiteAdmin, CancellationToken ct)
    {
        var comment = await db.Comments.FirstOrDefaultAsync(c => c.CommentId == commentId, ct);

        if (comment is null) return Result.NotFound();

        // The author, or the admin of the site the comment sits on — that is what
        // moderation is. Admin alone is not enough: it must be their site.
        var canDelete = comment.UserId == userId ||
                        (isSiteAdmin &&
                         await db.Sites.AnyAsync(s => s.SiteId == comment.SiteId && s.OwnerUserId == userId, ct));

        if (!canDelete) return Result.Forbidden();

        db.Comments.Remove(comment);
        await db.SaveChangesAsync(ct);

        return Result.Ok();
    }
}
