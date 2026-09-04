using Backend.Data;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;

namespace Backend.Features.Comments;

public sealed class GetCommentByIdRequest
{
    public Guid Id { get; set; }
}

public sealed class GetCommentByIdEndpoint : Endpoint<GetCommentByIdRequest, CommentResponse>
{
    public AppDbContext Db { get; set; } = default!;

    public override void Configure()
    {
        Get("/{id}");
        Group<CommentGroup>();
        AllowAnonymous();
    }

    public override async Task HandleAsync(GetCommentByIdRequest req, CancellationToken ct)
    {
        var comment = await Db.Comments
            .AsNoTracking()
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.CommentId == req.Id, ct);

        if (comment is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(CommentResponse.From(comment), ct);
    }
}
