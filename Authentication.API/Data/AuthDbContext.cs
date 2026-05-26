using Authentication.API.Models;
using Microsoft.EntityFrameworkCore;
using Shared.CL.Models;
namespace Authentication.API.Data;

public class AuthDbContext : DbContext
{
    public AuthDbContext(DbContextOptions<AuthDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    // <summary>Set this before SaveChangesAsync to stamp the current user on audit rows.</summary>
    public long? CurrentUserID { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Role>(e =>
        {
            e.HasKey(r => r.RoleID);
            e.Property(r => r.RoleID).ValueGeneratedNever(); // seeder inserts explicit IDs 1-6
            e.Property(r => r.RoleName).HasMaxLength(100).IsRequired();
        });

        modelBuilder.Entity<User>(e =>
        {
            e.HasKey(u => u.UserID);
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.IsActive).HasDefaultValue(true);
            e.HasOne(u => u.RoleNavigation).WithMany(r => r.Users)
             .HasForeignKey(u => u.RoleID).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AuditLog>(e =>
        {
            e.HasKey(a => a.AuditID);
            e.HasIndex(a => a.UserID);
            e.HasOne(a => a.User).WithMany(u => u.AuditLogs)
             .HasForeignKey(a => a.UserID).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AuditEntry>(b =>
        {
            b.HasKey(a => a.AuditEntryID);
            b.Property(a => a.EntityName).HasMaxLength(100).IsRequired();
            b.Property(a => a.PrimaryKey).HasMaxLength(100);
            b.Property(a => a.Action).HasMaxLength(20).IsRequired();
            b.HasIndex(a => a.ChangedAt);
            b.HasIndex(a => a.EntityName);
        });

        base.OnModelCreating(modelBuilder);
    }

    public new async Task<int> SaveChangesAsync()
    {
        var changedEntries = ChangeTracker.Entries()
            .Where(e => e.Entity is not AuditEntry && e.Entity is not AuditLog &&
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
