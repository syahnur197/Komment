using Backend.Services;
using FastEndpoints;
using FluentValidation;

namespace Backend.Features.Comments;

public sealed class GetAllCommentsRequest
{
    // Which blog is asking. Required — comments are never global.
    public string Site { get; set; } = default!;

    // The blog page's own slug; omit to get the whole site's comments.
    public string? PostSlug { get; set; }
}

public sealed class GetAllCommentsValidator : Validator<GetAllCommentsRequest>
{
    public GetAllCommentsValidator() => RuleFor(x => x.Site).NotEmpty();
}

// One class per endpoint (the REPR pattern: Request-Endpoint-Response). The
// endpoint binds and maps; CommentService decides.
public sealed class GetAllCommentsEndpoint : Endpoint<GetAllCommentsRequest, List<CommentResponse>>
{
    // Property injection — FastEndpoints fills this from the request scope.
    // No constructor, so adding a dependency is a one-line change.
    public CommentService Comments { get; set; } = default!;

    public override void Configure()
    {
        Get("");
        Group<CommentGroup>();
        AllowAnonymous();
    }

    public override async Task HandleAsync(GetAllCommentsRequest req, CancellationToken ct) =>
        await Send.OkAsync(await Comments.ListAsync(req.Site, req.PostSlug, ct), ct);
}
