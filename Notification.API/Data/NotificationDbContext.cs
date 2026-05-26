using Microsoft.EntityFrameworkCore;
using Notification.API.Models;
using Shared.CL.Models;
namespace Notification.API.Data;

public class NotificationDbContext : DbContext
{
    public NotificationDbContext(DbContextOptions<NotificationDbContext> options) : base(options) { }

    public DbSet<NotificationRecord> Notifications => Set<NotificationRecord>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    /// <summary>Set this before SaveChangesAsync to stamp the current user on audit rows.</summary>
    public long? CurrentUserID { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<NotificationRecord>(e =>
        {
            e.ToTable("Notifications");
            e.HasKey(n => n.NotificationID);
            e.Property(n => n.Message).HasMaxLength(1000).IsRequired();
            e.Property(n => n.Category).HasMaxLength(50).IsRequired();
            e.Property(n => n.Status).HasMaxLength(20).IsRequired();
            e.HasIndex(n => n.UserID);
            e.HasIndex(n => n.Status);
            e.HasIndex(n => n.Category);
            e.HasIndex(n => n.CreatedDate);
            e.HasIndex(n => new { n.UserID, n.Status });
        });

        // AuditEntries table is owned by Authentication.API in GovernanceDb — do not re-create it.
        modelBuilder.Entity<AuditEntry>()
            .ToTable("AuditEntries", t => t.ExcludeFromMigrations());
    }

    public new async Task<int> SaveChangesAsync()
    {
        var changedEntries = ChangeTracker.Entries()
            .Where(e => e.Entity is not AuditEntry &&
                        e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();

        foreach (var entry in changedEntries)
        {
            var action = entry.State switch
            {
                EntityState.Added => "Insert",
                EntityState.Modified => "Update",
                EntityState.Deleted => "Delete",
                _ => "Unknown"
            };
            var pk = entry.Properties
                .FirstOrDefault(p => p.Metadata.IsPrimaryKey())?.CurrentValue?.ToString();

            AuditEntries.Add(new AuditEntry
            {
                EntityName = entry.Entity.GetType().Name,
                PrimaryKey = pk,
                Action = action,
                ChangedByUserID = CurrentUserID,
                ChangedAt = DateTime.UtcNow
            });
        }

        return await base.SaveChangesAsync();
    }
}
