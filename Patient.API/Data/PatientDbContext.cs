using Microsoft.EntityFrameworkCore;
using Patient.API.Models;
using Shared.CL.Models;
namespace Patient.API.Data;

public class PatientDbContext : DbContext
{
    public PatientDbContext(DbContextOptions<PatientDbContext> options) : base(options) { }

    public DbSet<PatientRecord> Patients => Set<PatientRecord>();
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();
    public DbSet<Visit> Visits => Set<Visit>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    /// <summary>Set this before SaveChangesAsync to stamp the current user on audit rows.</summary>
    public long? CurrentUserID { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<PatientRecord>(e =>
        {
            e.ToTable("Patients");
            e.HasKey(p => p.PatientID);
            e.Property(p => p.UserID).HasColumnType("bigint");
            e.Property(p => p.Name).HasMaxLength(200).IsRequired();
            e.Property(p => p.ContactInfo).HasMaxLength(500);
            e.Property(p => p.EnrollmentStatus).HasMaxLength(50).IsRequired();
            e.HasIndex(p => p.EnrollmentStatus);
            // One user account maps to at most one patient record
            e.HasIndex(p => p.UserID).IsUnique().HasFilter("[UserID] IS NOT NULL");
        });

        modelBuilder.Entity<Enrollment>(e =>
        {
            e.ToTable("Enrollments");
            e.HasKey(en => en.EnrollmentID);
            e.Property(en => en.Status).HasMaxLength(50).IsRequired();
            e.Property(en => en.WithdrawalReason).HasMaxLength(500);
            e.HasIndex(en => en.PatientID);
            e.HasIndex(en => en.SiteProtocolID);
            e.HasIndex(en => en.Status);
            e.HasOne(en => en.Patient)
             .WithMany(p => p.Enrollments)
             .HasForeignKey(en => en.PatientID)
             .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Visit>(e =>
        {
            e.ToTable("Visits");
            e.HasKey(v => v.VisitID);
            e.Property(v => v.Status).HasMaxLength(50).IsRequired();
            e.Property(v => v.Notes).HasMaxLength(1000);
            e.HasIndex(v => v.EnrollmentID);
            e.HasIndex(v => v.Status);
            e.HasIndex(v => v.VisitDate);
            e.HasOne(v => v.Enrollment)
             .WithMany(en => en.Visits)
             .HasForeignKey(v => v.EnrollmentID)
             .OnDelete(DeleteBehavior.Cascade);
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
