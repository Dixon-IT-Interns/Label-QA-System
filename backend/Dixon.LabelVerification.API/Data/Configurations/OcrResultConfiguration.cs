using Dixon.LabelVerification.API.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dixon.LabelVerification.API.Data.Configurations;

public sealed class OcrResultConfiguration : IEntityTypeConfiguration<OcrResult>
{
    public void Configure(EntityTypeBuilder<OcrResult> entity)
    {
        entity.ToTable("OCRResults");
        entity.Property(x => x.Engine).HasMaxLength(100).IsRequired();
        entity.Property(x => x.EngineVersion).HasMaxLength(100).IsRequired();
        entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        entity.Property(x => x.RawExtractedText).HasMaxLength(20000);
        entity.Property(x => x.ProcessedImagePath).HasMaxLength(1024);
        entity.Property(x => x.OverallConfidence).HasColumnType("float");
        entity.HasIndex(x => x.InspectionSessionId);
        entity.HasOne(x => x.InspectionSession).WithMany().HasForeignKey(x => x.InspectionSessionId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.InspectionImage).WithMany(x => x.OcrResults)
            .HasForeignKey(x => x.InspectionImageId).OnDelete(DeleteBehavior.Restrict);
    }
}
