using System.Security.Claims;
using FastEndpoints;

namespace Backend.Features.Auth;

public sealed record MeResponse(Guid UserId, string Email, string Name, string? AvatarUrl, bool IsSiteAdmin);

// The blog calls this on page load to decide between "post a comment" and
// "sign in with Google".
public sealed class MeEndpoint : EndpointWithoutRequest<MeResponse>
{
    public override void Configure()
    {
        Get("/api/auth/me");
    }

    public override Task HandleAsync(CancellationToken ct) =>
        Send.OkAsync(new MeResponse(
            Guid.Parse(User.FindFirstValue(UserClaims.UserId)!),
            User.FindFirstValue(ClaimTypes.Email)!,
            User.FindFirstValue(ClaimTypes.Name)!,
            User.FindFirstValue("picture"),
            UserClaims.IsSiteAdmin(User)), ct);
}
