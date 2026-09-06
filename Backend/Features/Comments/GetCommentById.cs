using Backend.Services;
using FastEndpoints;

namespace Backend.Features.Comments;

public sealed class GetCommentByIdRequest
{
    public Guid Id { get; set; }
}

public sealed class GetCommentByIdEndpoint : Endpoint<GetCommentByIdRequest, CommentResponse>
{
    public CommentService Comments { get; set; } = default!;

    public override void Configure()
    {
        Get("/{id}");
        Group<CommentGroup>();
        AllowAnonymous();
    }

    public override async Task HandleAsync(GetCommentByIdRequest req, CancellationToken ct)
    {
        var result = await Comments.GetAsync(req.Id, ct);

        if (!result.IsOk)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(result.Value!, ct);
    }
}
