using Dixon.LabelVerification.API.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dixon.LabelVerification.API.Data.Configurations;

public sealed class InspectionLogConfiguration : IEntityTypeConfiguration<InspectionLog>
{
    public void Configure(EntityTypeBuilder<InspectionLog> entity)
    {
        entity.ToTable("InspectionLogs");
        entity.Property(x => x.EventType).HasColumnName("Action").HasMaxLength(100).IsRequired();
        entity.Property(x => x.Details).HasColumnName("Description").HasMaxLength(2000).IsRequired();
        entity.Property(x => x.PreviousValue).HasMaxLength(4000);
        entity.Property(x => x.NewValue).HasMaxLength(4000);
        entity.Property(x => x.IPAddress).HasMaxLength(45);
        entity.HasIndex(x => new { x.InspectionSessionId, x.CreatedAtUtc });
        entity.HasOne(x => x.InspectionSession).WithMany(x => x.Logs)
            .HasForeignKey(x => x.InspectionSessionId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.UserAccount).WithMany(x => x.InspectionLogs)
            .HasForeignKey(x => x.UserAccountId).OnDelete(DeleteBehavior.Restrict);
    }
}
