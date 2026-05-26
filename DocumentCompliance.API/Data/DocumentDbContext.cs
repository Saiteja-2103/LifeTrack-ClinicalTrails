using DocumentCompliance.API.Models;
using Microsoft.EntityFrameworkCore;
using Shared.CL.Models;
namespace DocumentCompliance.API.Data;

public class DocumentDbContext : DbContext
{
    public DocumentDbContext(DbContextOptions<DocumentDbContext> options) : base(options) { }

    public DbSet<Document> Documents => Set<Document>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    /// <summary>Set this before SaveChangesAsync to stamp the current user on audit rows.</summary>
    public long? CurrentUserID { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Document>(e =>
        {
            e.ToTable("Documents");
            e.HasKey(d => d.DocumentID);
            e.Property(d => d.Type).HasMaxLength(100).IsRequired();
            e.Property(d => d.Version).HasMaxLength(20).IsRequired();
            e.Property(d => d.Status).HasMaxLength(50).IsRequired();
            e.HasIndex(d => d.ProtocolID);
            e.HasIndex(d => d.Status);
            e.HasIndex(d => d.UploadedBy);
            e.HasIndex(d => d.UploadedAt);
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
