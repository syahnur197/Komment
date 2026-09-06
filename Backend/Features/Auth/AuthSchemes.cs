namespace Backend.Features.Auth;

// One app, two audiences, two cookies. A blog reader's cookie has to be
// third-party (SameSite=None) because the blog is on another origin; the admin's
// does not, and making it Lax keeps it working over plain HTTP locally. Program.cs
// forwards to one or the other by path, so no handler sees the wrong cookie.
public static class AuthSchemes
{
    public const string ByPath = "by-path";
    public const string Reader = "reader";
    public const string Admin = "admin";
}
