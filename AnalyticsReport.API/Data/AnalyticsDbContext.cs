using AnalyticsReport.API.Models;
using Microsoft.EntityFrameworkCore;
using Shared.CL.Models;
namespace AnalyticsReport.API.Data;

public class AnalyticsDbContext : DbContext
{
    public AnalyticsDbContext(DbContextOptions<AnalyticsDbContext> options) : base(options) { }

    public DbSet<KpiReport> KpiReports => Set<KpiReport>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    /// <summary>Set this before SaveChangesAsync to stamp the current user on audit rows.</summary>
    public long? CurrentUserID { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<KpiReport>(e =>
        {
            e.ToTable("KPIReports");
            e.HasKey(r => r.ReportID);
            e.Property(r => r.Scope).HasMaxLength(200).IsRequired();
            e.HasIndex(r => r.GeneratedDate);
            e.HasIndex(r => r.Scope);
        });

        // AuditEntries table is owned by ProtocolSite.API in ClinicalDb — do not re-create it.
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
