using Backend.Data;
using Backend.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

// Accounts, both kinds. Readers arrive through Google and are upserted on the
// way back from the OAuth callback; site admins register with a username and
// password. One Users table, one set of claims (UserClaims.For).
public sealed class AccountService(AppDbContext db, IConfiguration cfg)
{
    public async Task<Result<User>> RegisterAsync(
        string username, string email, string name, string password, CancellationToken ct)
    {
        //   MULTI_TENANCY=true  — SaaS: anyone may sign up and register their sites.
        //   MULTI_TENANCY=false — self-hosted: the first registration takes the box
        //                         and every one after it is refused.
        if (!cfg.GetValue("MULTI_TENANCY", false) && await db.Users.AnyAsync(u => u.IsSiteAdmin, ct))
            return Result<User>.Forbidden();

        if (await db.Users.AnyAsync(u => u.Username == username, ct))
            return Result<User>.Invalid(nameof(User.Username), "That username is taken.");

        var user = new User { Username = username, Email = email, Name = name, IsSiteAdmin = true };
        user.PasswordHash = new PasswordHasher<User>().HashPassword(user, password);

        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        return Result<User>.Ok(user);
    }

    // Null for "no such user" and for "wrong password" alike — the difference is a
    // free account-enumeration oracle, so callers cannot report one either.
    public async Task<User?> VerifyPasswordAsync(string username, string password, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Username == username, ct);

        var verified = user?.PasswordHash is not null &&
                       new PasswordHasher<User>().VerifyHashedPassword(user, user.PasswordHash, password)
                           is not PasswordVerificationResult.Failed;

        return verified ? user : null;
    }

    // Google's "sub" is the identity — stable per account, unlike email, which is
    // why a returning reader is matched on it and their profile refreshed.
    public async Task<User> UpsertGoogleAsync(
        string googleId, string email, string? name, string? avatarUrl, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.GoogleId == googleId, ct);

        if (user is null)
        {
            user = new User { GoogleId = googleId, Email = email, Name = name ?? email };
            db.Users.Add(user);
        }
        else
        {
            user.Email = email;
            user.Name = name ?? user.Name;
        }

        user.AvatarUrl = avatarUrl ?? user.AvatarUrl;

        await db.SaveChangesAsync(ct);

        return user;
    }
}
