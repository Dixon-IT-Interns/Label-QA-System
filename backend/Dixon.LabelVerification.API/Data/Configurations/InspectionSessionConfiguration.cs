using Dixon.LabelVerification.API.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dixon.LabelVerification.API.Data.Configurations;

public sealed class InspectionSessionConfiguration : IEntityTypeConfiguration<InspectionSession>
{
    public void Configure(EntityTypeBuilder<InspectionSession> entity)
    {
        entity.ToTable("InspectionSessions");
        entity.Property(x => x.InspectionNumber).HasMaxLength(40).IsRequired();
        entity.Property(x => x.ProductionOrderNumber).HasMaxLength(100);
        entity.Property(x => x.WorkOrderNumber).HasMaxLength(100);
        entity.Property(x => x.SerialNumber).HasMaxLength(200);
        entity.Property(x => x.FinalRemarks).HasMaxLength(2000);
        entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        entity.Property(x => x.COStatus).HasConversion<string>().HasMaxLength(32);
        entity.Property(x => x.ConfigStatus).HasConversion<string>().HasMaxLength(32);
        entity.Property(x => x.SerialStatus).HasConversion<string>().HasMaxLength(32);
        entity.Property(x => x.StartedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");
        entity.Property(x => x.RowVersion).IsRowVersion();
        entity.HasIndex(x => x.InspectionNumber).IsUnique();
        entity.HasIndex(x => new { x.BrandId, x.ModelId });
        entity.HasIndex(x => x.CreatedAtUtc);
        entity.HasIndex(x => new { x.Status, x.CreatedAtUtc });
        entity.HasOne(x => x.Brand).WithMany().HasForeignKey(x => x.BrandId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.Model).WithMany(x => x.InspectionSessions)
            .HasForeignKey(x => new { x.BrandId, x.ModelId })
            .HasPrincipalKey(x => new { x.BrandId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.CreatedByUser).WithMany(x => x.CreatedInspections)
            .HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.SetNull);
        entity.HasOne(x => x.LabelProfileCO).WithMany().HasForeignKey(x => x.LabelProfileCOId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.LabelProfileConfig).WithMany().HasForeignKey(x => x.LabelProfileConfigId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
