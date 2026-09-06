using Backend.Features.Auth;
using Backend.Services;
using FastEndpoints;

namespace Backend.Features.Comments;

public sealed class DeleteCommentRequest
{
    public Guid Id { get; set; }
}

// Endpoint<TRequest> — a request, no response body.
public sealed class DeleteCommentEndpoint : Endpoint<DeleteCommentRequest>
{
    public CommentService Comments { get; set; } = default!;

    public override void Configure()
    {
        Delete("/{id}");
        Group<CommentGroup>();
    }

    public override async Task HandleAsync(DeleteCommentRequest req, CancellationToken ct)
    {
        var result = await Comments.DeleteAsync(
            req.Id, UserClaims.UserIdOf(User)!.Value, UserClaims.IsSiteAdmin(User), ct);

        switch (result.Kind)
        {
            case ResultKind.NotFound:
                await Send.NotFoundAsync(ct);
                return;

            case ResultKind.Forbidden:
                await Send.ForbiddenAsync(ct);
                return;
        }

        await Send.NoContentAsync(ct);
    }
}
