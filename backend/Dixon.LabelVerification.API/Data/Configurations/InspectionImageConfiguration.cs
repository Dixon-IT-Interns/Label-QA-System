using Dixon.LabelVerification.API.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dixon.LabelVerification.API.Data.Configurations;

public sealed class InspectionImageConfiguration : IEntityTypeConfiguration<InspectionImage>
{
    public void Configure(EntityTypeBuilder<InspectionImage> entity)
    {
        entity.ToTable("InspectionImages", table =>
            table.HasCheckConstraint("CK_InspectionImages_SizeBytes", "[SizeBytes] > 0"));
        entity.Property(x => x.ImageType).HasConversion<string>().HasMaxLength(30);
        entity.Property(x => x.LabelType).HasConversion<string>().HasMaxLength(20);
        entity.Property(x => x.Role).HasConversion<string>().HasMaxLength(20);
        entity.Property(x => x.OriginalFileName).HasMaxLength(255).IsRequired();
        entity.Property(x => x.StoredFileName).HasMaxLength(100).IsRequired();
        entity.Property(x => x.StoragePath).HasMaxLength(1024).IsRequired();
        entity.Property(x => x.Sha256Hash).HasMaxLength(64).IsRequired();
        entity.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
        entity.Property(x => x.ImageQualityStatus).HasMaxLength(30).IsRequired();
        entity.Property(x => x.UploadedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");
        entity.HasIndex(x => new { x.InspectionSessionId, x.ImageType }).IsUnique();
        entity.HasIndex(x => x.InspectionSessionId);
        entity.HasOne(x => x.InspectionSession).WithMany(x => x.Images)
            .HasForeignKey(x => x.InspectionSessionId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.UploadedByUser).WithMany()
            .HasForeignKey(x => x.UploadedByUserId).OnDelete(DeleteBehavior.SetNull);
    }
}
