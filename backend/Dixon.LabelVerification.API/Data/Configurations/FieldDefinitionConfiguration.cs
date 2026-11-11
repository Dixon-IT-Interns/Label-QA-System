using Dixon.LabelVerification.API.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dixon.LabelVerification.API.Data.Configurations;

public sealed class FieldDefinitionConfiguration : IEntityTypeConfiguration<FieldDefinition>
{
    public void Configure(EntityTypeBuilder<FieldDefinition> entity)
    {
        entity.ToTable("FieldDefinitions", table =>
        {
            table.HasCheckConstraint("CK_FieldDefinitions_MinimumConfidence", "[OCRConfidenceThreshold] >= 0 AND [OCRConfidenceThreshold] <= 1");
            table.HasCheckConstraint("CK_FieldDefinitions_NumericAbsoluteTolerance", "[NumericAbsoluteTolerance] >= 0");
            table.HasCheckConstraint("CK_FieldDefinitions_NumericRelativeTolerance", "[NumericRelativeTolerance] >= 0 AND [NumericRelativeTolerance] <= 1");
        });
        entity.Property(x => x.FieldName).HasColumnName("FieldKey").HasMaxLength(100).IsRequired();
        entity.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
        entity.Property(x => x.DataType).HasMaxLength(50).HasDefaultValue("Text").IsRequired();
        entity.Property(x => x.Required).HasColumnName("IsRequired");
        entity.Property(x => x.Critical).HasColumnName("IsCritical");
        entity.Property(x => x.OcrConfidenceThreshold).HasColumnName("OCRConfidenceThreshold");
        entity.Property(x => x.ComparisonStrategy).HasConversion<string>().HasMaxLength(30);
        entity.Property(x => x.NumericAbsoluteTolerance).HasPrecision(18, 6);
        entity.Property(x => x.NumericRelativeTolerance).HasPrecision(18, 6);
        entity.HasIndex(x => new { x.LabelProfileId, x.FieldName }).IsUnique();
        entity.HasOne(x => x.LabelProfile).WithMany(x => x.FieldDefinitions)
            .HasForeignKey(x => x.LabelProfileId).OnDelete(DeleteBehavior.Restrict);
        entity.HasData(
            SeedField("73a3f07d-c906-4e49-a3db-4854ab1a9d10", "73a3f07d-c906-4e49-a3db-4854ab1a9d08",
                "MODEL", "Model", "Text", FieldComparisonStrategy.Identifier, 1),
            SeedField("73a3f07d-c906-4e49-a3db-4854ab1a9d11", "73a3f07d-c906-4e49-a3db-4854ab1a9d08",
                "PROCESSOR", "Processor", "Text", FieldComparisonStrategy.NormalizedText, 2),
            SeedField("73a3f07d-c906-4e49-a3db-4854ab1a9d12", "73a3f07d-c906-4e49-a3db-4854ab1a9d08",
                "STORAGE", "Storage", "Text", FieldComparisonStrategy.NormalizedText, 3),
            SeedField("73a3f07d-c906-4e49-a3db-4854ab1a9d13", "73a3f07d-c906-4e49-a3db-4854ab1a9d08",
                "MEMORY", "Memory", "Text", FieldComparisonStrategy.NormalizedText, 4),
            SeedField("73a3f07d-c906-4e49-a3db-4854ab1a9d14", "73a3f07d-c906-4e49-a3db-4854ab1a9d08",
                "DISPLAY", "Display", "Text", FieldComparisonStrategy.NormalizedText, 5),
            SeedField("73a3f07d-c906-4e49-a3db-4854ab1a9d15", "73a3f07d-c906-4e49-a3db-4854ab1a9d08",
                "BATTERY", "Battery", "Text", FieldComparisonStrategy.NormalizedText, 6),
            SeedField("73a3f07d-c906-4e49-a3db-4854ab1a9d20", "73a3f07d-c906-4e49-a3db-4854ab1a9d09",
                "PART_NUMBER", "Part Number", "Text", FieldComparisonStrategy.Identifier, 1),
            SeedField("73a3f07d-c906-4e49-a3db-4854ab1a9d21", "73a3f07d-c906-4e49-a3db-4854ab1a9d09",
                "PRODUCTION_ORDER", "Production Order", "Text", FieldComparisonStrategy.Identifier, 2),
            SeedField("73a3f07d-c906-4e49-a3db-4854ab1a9d22", "73a3f07d-c906-4e49-a3db-4854ab1a9d09",
                "WORK_ORDER", "Work Order", "Text", FieldComparisonStrategy.Identifier, 3));
    }

    private static FieldDefinition SeedField(
        string id,
        string profileId,
        string key,
        string displayName,
        string dataType,
        FieldComparisonStrategy comparisonStrategy,
        int displayOrder) => new()
    {
        Id = Guid.Parse(id),
        LabelProfileId = Guid.Parse(profileId),
        FieldName = key,
        DisplayName = displayName,
        DataType = dataType,
        Required = true,
        Critical = true,
        OcrConfidenceThreshold = 0.5,
        DisplayOrder = displayOrder,
        IsActive = true,
        ComparisonStrategy = comparisonStrategy,
        CreatedAtUtc = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero)
    };
}
