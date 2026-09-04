using Backend.Data;
using Backend.Entities;
using Backend.Features.Auth;
using FastEndpoints;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Backend.Features.Comments;

public sealed class CreateCommentRequest
{
    public string Site { get; set; } = default!;
    public string PostSlug { get; set; } = default!;
    public string Body { get; set; } = default!;

    // Omit for a top-level comment; set it to reply to an existing one.
    public Guid? ParentCommentId { get; set; }
}

// FluentValidation is built in. FastEndpoints discovers this by its request type
// and runs it before HandleAsync — a failure short-circuits with a 400.
public sealed class CreateCommentValidator : Validator<CreateCommentRequest>
{
    public CreateCommentValidator()
    {
        RuleFor(x => x.Site).NotEmpty().MaximumLength(100);
        RuleFor(x => x.PostSlug).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Body).NotEmpty().MaximumLength(4000);
    }
}

public sealed class CreateCommentEndpoint : Endpoint<CreateCommentRequest, CommentResponse>
{
    public AppDbContext Db { get; set; } = default!;

    public override void Configure()
    {
        Post("");
        Group<CommentGroup>();
    }

    public override async Task HandleAsync(CreateCommentRequest req, CancellationToken ct)
    {
        var siteId = await Db.Sites
            .Where(s => s.Slug == req.Site)
            .Select(s => s.SiteId)
            .FirstOrDefaultAsync(ct);

        if (siteId == Guid.Empty)
        {
            AddError(r => r.Site, "No such site.");
            await Send.ErrorsAsync(cancellation: ct);
            return;
        }

        if (req.ParentCommentId is { } parentId)
        {
            // A reply has to hang off a real comment on the same post of the
            // same site, or the thread the blog renders makes no sense.
            var parentExists = await Db.Comments.AnyAsync(
                c => c.CommentId == parentId && c.SiteId == siteId && c.PostSlug == req.PostSlug, ct);

            if (!parentExists)
            {
                AddError(r => r.ParentCommentId, "No such comment on this post.");
                await Send.ErrorsAsync(cancellation: ct);
                return;
            }
        }

        var comment = new Comment
        {
            SiteId = siteId,
            PostSlug = req.PostSlug,
            Body = req.Body,
            ParentCommentId = req.ParentCommentId,
            UserId = UserClaims.UserIdOf(User)!.Value,
        };

        Db.Comments.Add(comment);
        await Db.SaveChangesAsync(ct);
        await Db.Entry(comment).Reference(c => c.User).LoadAsync(ct);

        // Location header points at the endpoint type, not a route name string.
        await Send.CreatedAtAsync<GetCommentByIdEndpoint>(
            new { Id = comment.CommentId }, CommentResponse.From(comment), cancellation: ct);
    }
}
