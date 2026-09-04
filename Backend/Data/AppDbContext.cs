namespace Backend.Data;

using Microsoft.EntityFrameworkCore;
using Entities;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    private void Timestamp()
    {
        foreach (var entry in ChangeTracker.Entries<ITimestamped>())
        {
            if (entry.State == EntityState.Added)
                entry.Entity.CreatedAt = DateTime.UtcNow;

            if (entry.State is EntityState.Added or EntityState.Modified)
                entry.Entity.UpdatedAt = DateTime.UtcNow;
        }
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        Timestamp();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken ct = default)
    {
        Timestamp();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, ct);
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        // Postgres treats NULLs as distinct, so these stay unique without a
        // filter even though most rows have one of the two set and not the other.
        b.Entity<User>().HasIndex(u => u.GoogleId).IsUnique();
        b.Entity<User>().HasIndex(u => u.Username).IsUnique();
        b.Entity<Site>().HasIndex(s => s.Slug).IsUnique();

        // Every comment lookup is "this site, this page".
        b.Entity<Comment>().HasIndex(c => new { c.SiteId, c.PostSlug });

        // Deleting a comment takes its replies with it — the alternative is
        // orphaned rows pointing at nothing.
        b.Entity<Comment>()
            .HasMany(c => c.Replies)
            .WithOne(c => c.Parent!)
            .HasForeignKey(c => c.ParentCommentId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    public DbSet<Site> Sites => Set<Site>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Comment> Comments => Set<Comment>();
}
