using Backend.Features.Auth;
using Backend.Services;
using FastEndpoints;
using FluentValidation;
using FluentValidation.Results;

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
// and runs it before HandleAsync — a failure short-circuits with a 400. Shape
// only; whether the site or parent exists is CommentService's to answer.
public sealed class CreateCommentValidator : Validator<CreateCommentRequest>
{
    public CreateCommentValidator()
    {
        RuleFor(x => x.Site).NotEmpty().MaximumLength(100);
        RuleFor(x => x.PostSlug).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Body).NotEmpty().MaximumLength(4000);
    }
}

public sealed class CreateCommentEndpoint(CommentService commentService) : Endpoint<CreateCommentRequest, CommentResponse>
{
    private readonly CommentService _commentService = commentService;

    public override void Configure()
    {
        Post("");
        Group<CommentGroup>();
    }

    public override async Task HandleAsync(CreateCommentRequest req, CancellationToken ct)
    {
        var result = await _commentService.CreateAsync(
            UserClaims.UserIdOf(User)!.Value, req.Site, req.PostSlug, req.Body, req.ParentCommentId, ct);

        if (!result.IsOk)
        {
            ValidationFailures.Add(new ValidationFailure(result.Field!, result.Message!));
            await Send.ErrorsAsync(cancellation: ct);
            return;
        }

        // Location header points at the endpoint type, not a route name string.
        await Send.CreatedAtAsync<GetCommentByIdEndpoint>(
            new { Id = result.Value!.CommentId }, result.Value, cancellation: ct);
    }
}
