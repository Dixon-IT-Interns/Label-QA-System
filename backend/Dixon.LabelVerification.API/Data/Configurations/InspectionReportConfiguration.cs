using Dixon.LabelVerification.API.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dixon.LabelVerification.API.Data.Configurations;

public sealed class InspectionReportConfiguration : IEntityTypeConfiguration<InspectionReport>
{
    public void Configure(EntityTypeBuilder<InspectionReport> entity)
    {
        entity.ToTable("InspectionReports");
        entity.Property(x => x.ReportNumber).HasMaxLength(50).IsRequired();
        entity.Property(x => x.ReportFilePath).HasMaxLength(1024).IsRequired();
        entity.Property(x => x.Sha256Hash).HasMaxLength(64).IsRequired();
        entity.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
        entity.Property(x => x.GeneratedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");
        entity.HasIndex(x => x.ReportNumber).IsUnique();
        entity.HasIndex(x => x.InspectionSessionId);
        entity.HasOne(x => x.InspectionSession).WithMany(x => x.Reports)
            .HasForeignKey(x => x.InspectionSessionId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.GeneratedByUser).WithMany().HasForeignKey(x => x.GeneratedByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
