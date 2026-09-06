using Backend.Services;
using FastEndpoints;

namespace Backend.Features.Comments;

public sealed class GetCommentByIdRequest
{
    public Guid Id { get; set; }
}

public sealed class GetCommentByIdEndpoint(CommentService commentService) : Endpoint<GetCommentByIdRequest, CommentResponse>
{
    private readonly CommentService _commentService = commentService;

    public override void Configure()
    {
        Get("/{id}");
        Group<CommentGroup>();
        AllowAnonymous();
    }

    public override async Task HandleAsync(GetCommentByIdRequest req, CancellationToken ct)
    {
        var result = await _commentService.GetAsync(req.Id, ct);

        if (!result.IsOk)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(result.Value!, ct);
    }
}
