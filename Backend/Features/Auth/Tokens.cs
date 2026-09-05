using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Backend.Entities;
using Microsoft.IdentityModel.Tokens;

namespace Backend.Features.Auth;

// Two kinds of client, two kinds of credential. A blog is a page on someone
// else's origin, so its readers carry the cross-site session cookie. The admin
// console is a browser app calling this API directly, and a third-party cookie
// is exactly what Safari and Firefox drop — so it carries a bearer token
// instead. Both are minted from UserClaims.For, so past authentication no
// endpoint can tell which one it got.
public static class Tokens
{
    public const string Issuer = "komment";
    public const string Audience = "komment";

    // ponytail: access token only, no refresh and no revocation list. Seven days
    // trades "sign in every morning" against "a stolen token stays useful for a
    // week". Add refresh tokens plus a revoked-token table when that stops being
    // an acceptable trade — signing out is currently client-side only.
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    // HS256 needs at least 256 bits of key. There is deliberately no hard-coded
    // fallback: a default in the source is a default anyone can read, and this
    // key mints admin tokens. Development gets a random per-process key so
    // `dotnet run` still works out of the box — it changes on every restart, so
    // tokens do not survive one.
    public static SymmetricSecurityKey SigningKey(IConfiguration cfg, IHostEnvironment env)
    {
        var configured = cfg["JWT_SIGNING_KEY"];

        if (!string.IsNullOrWhiteSpace(configured) && Encoding.UTF8.GetByteCount(configured) >= 32)
            return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configured));

        if (!env.IsDevelopment())
            throw new InvalidOperationException(
                "JWT_SIGNING_KEY must be set to at least 32 bytes outside Development. " +
                "Generate one with: openssl rand -base64 48");

        return new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(32));
    }

    public static (string Token, DateTime ExpiresAt) Create(User user, SymmetricSecurityKey key)
    {
        var expiresAt = DateTime.UtcNow.Add(Lifetime);

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: UserClaims.For(user),
            expires: expiresAt,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }

    // Claims go onto the token under the exact names UserClaims uses, so
    // validation must not rewrite them on the way back in (MapInboundClaims is
    // off in Program.cs for the same reason).
    public static TokenValidationParameters Validation(SymmetricSecurityKey key) => new()
    {
        ValidIssuer = Issuer,
        ValidAudience = Audience,
        IssuerSigningKey = key,
        ValidateIssuerSigningKey = true,
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromSeconds(30),
        NameClaimType = ClaimTypes.Name,
        RoleClaimType = ClaimTypes.Role,
    };
}
