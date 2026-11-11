using Dixon.LabelVerification.API.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dixon.LabelVerification.API.Data.Configurations;

public sealed class ExtractedFieldConfiguration : IEntityTypeConfiguration<ExtractedField>
{
    public void Configure(EntityTypeBuilder<ExtractedField> entity)
    {
        entity.ToTable("ExtractedFields");
        entity.Property(x => x.FieldKey).HasMaxLength(100).IsRequired();
        entity.Property(x => x.ExtractedValue).HasMaxLength(2000);
        entity.Property(x => x.NormalizedValue).HasMaxLength(2000);
        entity.Property(x => x.Confidence).HasColumnType("float");
        entity.Property(x => x.BoundingBoxJson).HasMaxLength(2000);
        entity.Property(x => x.CorrectionReason).HasMaxLength(1000);
        entity.HasIndex(x => new { x.OcrResultId, x.FieldKey });
        entity.HasOne(x => x.OcrResult).WithMany(x => x.ExtractedFields)
            .HasForeignKey(x => x.OcrResultId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.FieldDefinition).WithMany().HasForeignKey(x => x.FieldDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.CorrectedByUser).WithMany().HasForeignKey(x => x.CorrectedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
