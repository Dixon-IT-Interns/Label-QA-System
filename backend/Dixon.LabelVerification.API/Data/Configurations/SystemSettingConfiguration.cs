using Dixon.LabelVerification.API.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dixon.LabelVerification.API.Data.Configurations;

public sealed class SystemSettingConfiguration : IEntityTypeConfiguration<SystemSetting>
{
    public void Configure(EntityTypeBuilder<SystemSetting> entity)
    {
        entity.ToTable("SystemSettings");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.SettingKey).HasMaxLength(150).IsRequired();
        entity.Property(x => x.SettingValue).HasMaxLength(2000);
        entity.Property(x => x.Description).HasMaxLength(500);
        entity.Property(x => x.UpdatedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");
        entity.HasIndex(x => x.SettingKey).IsUnique();
        entity.HasData(
            new SystemSetting
            {
                Id = Guid.Parse("73a3f07d-c906-4e49-a3db-4854ab1a9d21"),
                SettingKey = "OcrMinimumConfidence",
                SettingValue = "0.85",
                Description = "Minimum OCR confidence required before configured field values can be compared.",
                UpdatedAtUtc = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero)
            },
            new SystemSetting
            {
                Id = Guid.Parse("73a3f07d-c906-4e49-a3db-4854ab1a9d22"),
                SettingKey = "InspectionStatuses",
                SettingValue = "[\"READY\",\"PROCESSING\",\"PASS\",\"FAIL\",\"REVIEW_REQUIRED\",\"BLOCKED\",\"CANCELLED\",\"ERROR\",\"NOT_APPLICABLE\"]",
                Description = "Status values supported by inspection and stage results.",
                UpdatedAtUtc = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero)
            });
    }
}
