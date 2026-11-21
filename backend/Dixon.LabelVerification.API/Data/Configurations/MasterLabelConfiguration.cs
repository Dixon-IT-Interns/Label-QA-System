using Dixon.LabelVerification.API.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dixon.LabelVerification.API.Data.Configurations;

public sealed class MasterLabelConfiguration : IEntityTypeConfiguration<MasterLabel>
{
    public void Configure(EntityTypeBuilder<MasterLabel> entity)
    {
        entity.ToTable("MasterLabels", table =>
        {
            table.HasCheckConstraint("CK_MasterLabels_LabelType", "[LabelType] IN ('CO', 'CONFIG')");
            table.HasCheckConstraint("CK_MasterLabels_SizeBytes", "[SizeBytes] > 0");
        });
        entity.Property(x => x.LabelType).HasConversion<string>().HasMaxLength(20);
        entity.Property(x => x.OriginalFileName).HasMaxLength(255).IsRequired();
        entity.Property(x => x.StoredFileName).HasMaxLength(100).IsRequired();
        entity.Property(x => x.StoragePath).HasMaxLength(1024).IsRequired();
        entity.Property(x => x.Sha256Hash).HasMaxLength(64).IsRequired();
        entity.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
        entity.Property(x => x.Engine).HasMaxLength(100).IsRequired();
        entity.Property(x => x.EngineVersion).HasMaxLength(100).IsRequired();
        entity.Property(x => x.OcrStatus).HasConversion<string>().HasMaxLength(32);
        entity.Property(x => x.ImageQualityStatus).HasMaxLength(30).IsRequired();
        entity.Property(x => x.OverallConfidence).HasColumnType("float");
        entity.Property(x => x.UploadedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");
        entity.HasIndex(x => new { x.ModelId, x.LabelType }).IsUnique();
        entity.HasOne(x => x.Model).WithMany()
            .HasForeignKey(x => new { x.BrandId, x.ModelId })
            .HasPrincipalKey(x => new { x.BrandId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.LabelProfile).WithMany()
            .HasForeignKey(x => x.LabelProfileId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.UploadedByUser).WithMany()
            .HasForeignKey(x => x.UploadedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
