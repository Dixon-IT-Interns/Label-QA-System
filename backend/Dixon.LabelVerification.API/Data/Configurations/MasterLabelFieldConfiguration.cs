using Dixon.LabelVerification.API.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dixon.LabelVerification.API.Data.Configurations;

public sealed class MasterLabelFieldConfiguration : IEntityTypeConfiguration<MasterLabelField>
{
    public void Configure(EntityTypeBuilder<MasterLabelField> entity)
    {
        entity.ToTable("MasterLabelFields");
        entity.Property(x => x.FieldKey).HasMaxLength(100).IsRequired();
        entity.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
        entity.Property(x => x.ExtractedValue).HasMaxLength(2000);
        entity.Property(x => x.NormalizedValue).HasMaxLength(2000);
        entity.Property(x => x.Confidence).HasColumnType("float");
        entity.Property(x => x.Reason).HasMaxLength(1000).IsRequired();
        entity.HasIndex(x => new { x.MasterLabelId, x.FieldKey }).IsUnique();
        entity.HasOne(x => x.MasterLabel).WithMany(x => x.Fields)
            .HasForeignKey(x => x.MasterLabelId).OnDelete(DeleteBehavior.Cascade);
    }
}
