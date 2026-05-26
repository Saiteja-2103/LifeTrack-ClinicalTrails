using Microsoft.EntityFrameworkCore;
using ProtocolSite.API.Models;
using Shared.CL.Models;
namespace ProtocolSite.API.Data;

public class ProtocolSiteDbContext : DbContext
{
    public ProtocolSiteDbContext(DbContextOptions<ProtocolSiteDbContext> options) : base(options) { }

    public DbSet<Protocol> Protocols => Set<Protocol>();
    public DbSet<Site> Sites => Set<Site>();
    public DbSet<SiteProtocol> SiteProtocols => Set<SiteProtocol>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    /// <summary>Set this before SaveChangesAsync to stamp the current user on audit rows.</summary>
    public long? CurrentUserID { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Protocol>(b =>
        {
            b.HasKey(p => p.ProtocolID);
            b.Property(p => p.Title).HasMaxLength(300).IsRequired();
            b.Property(p => p.Phase).HasMaxLength(50).IsRequired();
            b.Property(p => p.Status).HasMaxLength(50).IsRequired();
            b.HasIndex(p => p.Status);
            b.HasIndex(p => p.Phase);
        });

        modelBuilder.Entity<Site>(b =>
        {
            b.HasKey(s => s.SiteID);
            b.Property(s => s.Name).HasMaxLength(300).IsRequired();
            b.Property(s => s.Location).HasMaxLength(500).IsRequired();
            b.Property(s => s.Status).HasMaxLength(50).IsRequired();
            b.HasIndex(s => s.Status);
        });

        modelBuilder.Entity<SiteProtocol>(b =>
        {
            b.HasKey(sp => sp.SiteProtocolID);
            b.Property(sp => sp.Status).HasMaxLength(50).IsRequired();
            b.HasIndex(sp => sp.SiteID);
            b.HasIndex(sp => sp.ProtocolID);
            b.HasIndex(sp => sp.InvestigatorID);
            b.HasIndex(sp => sp.Status);
            b.HasOne(sp => sp.Site)
             .WithMany(s => s.SiteProtocols)
             .HasForeignKey(sp => sp.SiteID)
             .OnDelete(DeleteBehavior.Restrict);
            b.HasOne(sp => sp.Protocol)
             .WithMany(p => p.SiteProtocols)
             .HasForeignKey(sp => sp.ProtocolID)
             .OnDelete(DeleteBehavior.Restrict);
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
