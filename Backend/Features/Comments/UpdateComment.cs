using Backend.Features.Auth;
using Backend.Services;
using FastEndpoints;
using FluentValidation;

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
    public CommentService Comments { get; set; } = default!;

    public override void Configure()
    {
        Patch("/{id}");
        Group<CommentGroup>();
    }

    public override async Task HandleAsync(UpdateCommentRequest req, CancellationToken ct)
    {
        var result = await Comments.UpdateAsync(req.Id, UserClaims.UserIdOf(User)!.Value, req.Body, ct);

        switch (result.Kind)
        {
            case ResultKind.NotFound:
                await Send.NotFoundAsync(ct);
                return;

            case ResultKind.Forbidden:
                await Send.ForbiddenAsync(ct);
                return;
        }

        await Send.OkAsync(result.Value!, ct);
    }
}
