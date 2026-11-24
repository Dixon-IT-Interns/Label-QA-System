using Dixon.LabelVerification.API.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dixon.LabelVerification.API.Data;

public sealed class LabelVerificationDbContext(DbContextOptions<LabelVerificationDbContext> options)
    : DbContext(options)
{
    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<LaptopModel> Models => Set<LaptopModel>();
    public DbSet<LabelProfile> LabelProfiles => Set<LabelProfile>();
    public DbSet<FieldDefinition> FieldDefinitions => Set<FieldDefinition>();
    public DbSet<FieldAlias> FieldAliases => Set<FieldAlias>();
    public DbSet<MasterLabel> MasterLabels => Set<MasterLabel>();
    public DbSet<MasterLabelField> MasterLabelFields => Set<MasterLabelField>();
    public DbSet<User> Users => Set<User>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<InspectionSession> InspectionSessions => Set<InspectionSession>();
    public DbSet<InspectionImage> InspectionImages => Set<InspectionImage>();
    public DbSet<OcrResult> OcrResults => Set<OcrResult>();
    public DbSet<ExtractedField> ExtractedFields => Set<ExtractedField>();
    public DbSet<FieldComparison> FieldComparisons => Set<FieldComparison>();
    public DbSet<StageResult> StageResults => Set<StageResult>();
    public DbSet<InspectionLog> InspectionLogs => Set<InspectionLog>();
    public DbSet<InspectionReport> InspectionReports => Set<InspectionReport>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
    public DbSet<InspectionProcessingJob> InspectionProcessingJobs => Set<InspectionProcessingJob>();
    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LabelVerificationDbContext).Assembly);
        foreach (var entityType in modelBuilder.Model.GetEntityTypes()
                     .Where(x => typeof(AuditedEntity).IsAssignableFrom(x.ClrType)))
        {
            modelBuilder.Entity(entityType.ClrType)
                .Property(nameof(AuditedEntity.CreatedAtUtc))
                .HasDefaultValueSql("SYSUTCDATETIME()");
        }
    }

    public override int SaveChanges()
    {
        ApplyUtcTimestamps();
        return base.SaveChanges();
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplyUtcTimestamps();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyUtcTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        ApplyUtcTimestamps();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void ApplyUtcTimestamps()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var entry in ChangeTracker.Entries<AuditedEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAtUtc = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAtUtc = now;
                entry.Property(x => x.CreatedAtUtc).IsModified = false;
            }
        }
    }
}
