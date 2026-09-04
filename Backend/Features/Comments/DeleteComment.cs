using Backend.Data;
using Backend.Features.Auth;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;

namespace Backend.Features.Comments;

public sealed class DeleteCommentRequest
{
    public Guid Id { get; set; }
}

// Endpoint<TRequest> — a request, no response body.
public sealed class DeleteCommentEndpoint : Endpoint<DeleteCommentRequest>
{
    public AppDbContext Db { get; set; } = default!;

    public override void Configure()
    {
        Delete("/{id}");
        Group<CommentGroup>();
    }

    public override async Task HandleAsync(DeleteCommentRequest req, CancellationToken ct)
    {
        var comment = await Db.Comments.FirstOrDefaultAsync(c => c.CommentId == req.Id, ct);

        if (comment is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var userId = UserClaims.UserIdOf(User);

        // The author, or the admin of the site the comment sits on — that is
        // what moderation is.
        var canDelete = comment.UserId == userId ||
                        (UserClaims.IsSiteAdmin(User) &&
                         await Db.Sites.AnyAsync(s => s.SiteId == comment.SiteId && s.OwnerUserId == userId, ct));

        if (!canDelete)
        {
            await Send.ForbiddenAsync(ct);
            return;
        }

        Db.Comments.Remove(comment);
        await Db.SaveChangesAsync(ct);

        await Send.NoContentAsync(ct);
    }
}
