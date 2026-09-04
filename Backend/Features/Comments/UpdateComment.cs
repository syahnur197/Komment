using Backend.Data;
using Backend.Features.Auth;
using FastEndpoints;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Backend.Features.Comments;

// One DTO fed from two sources: Id comes from the route token, the rest from
// the JSON body. FastEndpoints binds both into the same object.
public sealed class UpdateCommentRequest
{
    public Guid Id { get; set; }
    public string Body { get; set; } = default!;
}

public sealed class UpdateCommentValidator : Validator<UpdateCommentRequest>
{
    public UpdateCommentValidator()
    {
        RuleFor(x => x.Body).NotEmpty().MaximumLength(4000);
    }
}

public sealed class UpdateCommentEndpoint : Endpoint<UpdateCommentRequest, CommentResponse>
{
    public AppDbContext Db { get; set; } = default!;

    public override void Configure()
    {
        Patch("/{id}");
        Group<CommentGroup>();
    }

    public override async Task HandleAsync(UpdateCommentRequest req, CancellationToken ct)
    {
        var comment = await Db.Comments.Include(c => c.User).FirstOrDefaultAsync(c => c.CommentId == req.Id, ct);

        if (comment is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        // Not business logic — a trust boundary. Anyone signed in can otherwise
        // rewrite anyone else's comment.
        if (comment.UserId != UserClaims.UserIdOf(User))
        {
            await Send.ForbiddenAsync(ct);
            return;
        }

        comment.Body = req.Body;
        await Db.SaveChangesAsync(ct);

        await Send.OkAsync(CommentResponse.From(comment), ct);
    }
}
