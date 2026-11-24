using Dixon.LabelVerification.API.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dixon.LabelVerification.API.Data.Configurations;

public sealed class ActivityLogConfiguration : IEntityTypeConfiguration<ActivityLog>
{
    public void Configure(EntityTypeBuilder<ActivityLog> entity)
    {
        entity.ToTable("ActivityLogs");
        entity.Property(x => x.UserName).HasMaxLength(150).IsRequired();
        entity.Property(x => x.Role).HasMaxLength(30).IsRequired();
        entity.Property(x => x.BrandName).HasMaxLength(200);
        entity.Property(x => x.EntityType).HasMaxLength(80);
        entity.Property(x => x.Action).HasMaxLength(100).IsRequired();
        entity.Property(x => x.Details).HasMaxLength(2000).IsRequired();
        entity.Property(x => x.IPAddress).HasMaxLength(45);
        entity.HasIndex(x => new { x.BrandId, x.CreatedAtUtc });
        entity.HasIndex(x => new { x.UserAccountId, x.CreatedAtUtc });
        entity.HasOne(x => x.UserAccount).WithMany().HasForeignKey(x => x.UserAccountId).OnDelete(DeleteBehavior.SetNull);
        entity.HasOne(x => x.Brand).WithMany().HasForeignKey(x => x.BrandId).OnDelete(DeleteBehavior.SetNull);
        entity.HasOne(x => x.InspectionSession).WithMany().HasForeignKey(x => x.InspectionSessionId).OnDelete(DeleteBehavior.SetNull);
    }
}
