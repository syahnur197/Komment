using System.Security.Claims;
using Backend.Entities;

namespace Backend.Features.Auth;

// Both sign-in paths — Google and username/password — end up issuing the same
// cookie, so both build their claims here.
public static class UserClaims
{
    public const string UserId = "uid";
    public const string SiteAdminRole = "site-admin";

    public static Guid? UserIdOf(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(UserId), out var id) ? id : null;

    public static bool IsSiteAdmin(ClaimsPrincipal principal) =>
        principal.IsInRole(SiteAdminRole);

    public static IEnumerable<Claim> For(User user)
    {
        yield return new Claim(UserId, user.UserId.ToString());
        yield return new Claim(ClaimTypes.Email, user.Email);
        yield return new Claim(ClaimTypes.Name, user.Name);

        if (user.IsSiteAdmin)
            yield return new Claim(ClaimTypes.Role, SiteAdminRole);
    }
}
