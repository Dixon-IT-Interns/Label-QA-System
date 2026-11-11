using Dixon.LabelVerification.API.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dixon.LabelVerification.API.Data.Configurations;

public sealed class FieldComparisonConfiguration : IEntityTypeConfiguration<FieldComparison>
{
    public void Configure(EntityTypeBuilder<FieldComparison> entity)
    {
        entity.ToTable("FieldComparisons", table =>
        {
            table.HasCheckConstraint("CK_FieldComparisons_MasterConfidence", "[MasterConfidence] IS NULL OR ([MasterConfidence] >= 0 AND [MasterConfidence] <= 1)");
            table.HasCheckConstraint("CK_FieldComparisons_ActualConfidence", "[ActualConfidence] IS NULL OR ([ActualConfidence] >= 0 AND [ActualConfidence] <= 1)");
        });
        entity.Property(x => x.FieldKey).HasMaxLength(100).IsRequired();
        entity.Property(x => x.MasterValue).HasMaxLength(2000);
        entity.Property(x => x.ActualValue).HasMaxLength(2000);
        entity.Property(x => x.NormalizedMasterValue).HasMaxLength(2000);
        entity.Property(x => x.NormalizedActualValue).HasMaxLength(2000);
        entity.Property(x => x.MismatchReason).HasMaxLength(1000);
        entity.Property(x => x.ComparisonStrategy).HasConversion<string>().HasMaxLength(30);
        entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        entity.HasIndex(x => new { x.StageResultId, x.FieldKey }).IsUnique();
        entity.HasOne(x => x.StageResult).WithMany(x => x.FieldComparisons)
            .HasForeignKey(x => x.StageResultId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.FieldDefinition).WithMany(x => x.Comparisons)
            .HasForeignKey(x => x.FieldDefinitionId).OnDelete(DeleteBehavior.Restrict);
    }
}
