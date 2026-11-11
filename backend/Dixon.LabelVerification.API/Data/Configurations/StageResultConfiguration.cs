using Dixon.LabelVerification.API.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dixon.LabelVerification.API.Data.Configurations;

public sealed class StageResultConfiguration : IEntityTypeConfiguration<StageResult>
{
    public void Configure(EntityTypeBuilder<StageResult> entity)
    {
        entity.ToTable("StageResults");
        entity.Property(x => x.StageType).HasConversion<string>().HasMaxLength(32);
        entity.Property(x => x.LabelType).HasConversion<string>().HasMaxLength(20);
        entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        entity.Property(x => x.Remarks).HasMaxLength(2000);
        entity.HasIndex(x => new { x.InspectionSessionId, x.StageType });
        entity.HasOne(x => x.InspectionSession).WithMany(x => x.StageResults)
            .HasForeignKey(x => x.InspectionSessionId).OnDelete(DeleteBehavior.Restrict);
    }
}
