using Dixon.LabelVerification.API.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dixon.LabelVerification.API.Data.Configurations;

public sealed class InspectionProcessingJobConfiguration : IEntityTypeConfiguration<InspectionProcessingJob>
{
    public void Configure(EntityTypeBuilder<InspectionProcessingJob> entity)
    {
        entity.ToTable("InspectionProcessingJobs");
        entity.Property(x => x.Status).HasMaxLength(30).IsRequired();
        entity.Property(x => x.LastError).HasMaxLength(2000);
        entity.HasIndex(x => new { x.Status, x.CreatedAtUtc });
        entity.HasIndex(x => x.InspectionSessionId).IsUnique();
        entity.HasOne(x => x.InspectionSession).WithOne()
            .HasForeignKey<InspectionProcessingJob>(x => x.InspectionSessionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
