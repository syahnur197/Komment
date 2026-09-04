using Backend.Data;
using FastEndpoints;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Backend.Features.Comments;

public sealed class GetAllCommentsRequest
{
    // Which blog is asking. Required — comments are never global.
    public string Site { get; set; } = default!;

    // The Hugo page's own slug; omit to get the whole site's comments.
    public string? PostSlug { get; set; }
}

public sealed class GetAllCommentsValidator : Validator<GetAllCommentsRequest>
{
    public GetAllCommentsValidator() => RuleFor(x => x.Site).NotEmpty();
}

// Flat list, oldest first, replies carry ParentCommentId. The blog nests them
// client-side — cheaper than shipping a tree builder and a depth limit here.
// One class per endpoint (the REPR pattern: Request-Endpoint-Response).
public sealed class GetAllCommentsEndpoint : Endpoint<GetAllCommentsRequest, List<CommentResponse>>
{
    // Property injection — FastEndpoints fills this from the request scope.
    // No constructor, so adding a dependency is a one-line change.
    public AppDbContext Db { get; set; } = default!;

    public override void Configure()
    {
        Get("");
        Group<CommentGroup>();
        AllowAnonymous();
    }

    public override async Task HandleAsync(GetAllCommentsRequest req, CancellationToken ct)
    {
        var comments = await Db.Comments
            .AsNoTracking()
            .Where(c => c.Site.Slug == req.Site)
            .Where(c => req.PostSlug == null || c.PostSlug == req.PostSlug)
            .OrderBy(c => c.CreatedAt)
            .Include(c => c.User)
            .ToListAsync(ct);

        await Send.OkAsync(comments.Select(CommentResponse.From).ToList(), ct);
    }
}
