using Dixon.LabelVerification.API.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dixon.LabelVerification.API.Data.Configurations;

public sealed class LabelProfileConfiguration : IEntityTypeConfiguration<LabelProfile>
{
    public void Configure(EntityTypeBuilder<LabelProfile> entity)
    {
        entity.ToTable("LabelProfiles");
        entity.Property(x => x.Code).HasColumnName("ProfileName").HasMaxLength(100).IsRequired();
        entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
        entity.Property(x => x.Version).HasColumnName("ProfileVersion");
        entity.Property(x => x.LabelType).HasConversion<string>().HasMaxLength(20);
        entity.Property(x => x.EffectiveFromUtc).HasDefaultValueSql("SYSUTCDATETIME()");
        entity.HasIndex(x => new { x.BrandId, x.ModelId, x.LabelType, x.Code, x.Version })
            .IsUnique().HasFilter("[ModelId] IS NOT NULL");
        entity.HasIndex(x => new { x.BrandId, x.LabelType, x.Code, x.Version })
            .IsUnique().HasFilter("[ModelId] IS NULL");
        entity.HasIndex(x => new { x.ModelId, x.LabelType }).IsUnique()
            .HasFilter("[IsActive] = 1 AND [ModelId] IS NOT NULL");
        entity.HasOne(x => x.Brand).WithMany(x => x.LabelProfiles)
            .HasForeignKey(x => x.BrandId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.Model).WithMany(x => x.LabelProfiles)
            .HasForeignKey(x => new { x.BrandId, x.ModelId })
            .HasPrincipalKey(x => new { x.BrandId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasData(
            SeedProfile("73a3f07d-c906-4e49-a3db-4854ab1a9d06", LabelType.CO,
                "HP-15-FD1652TU-CO", "HP 15-fd1652TU CO", 1, false),
            SeedProfile("73a3f07d-c906-4e49-a3db-4854ab1a9d07", LabelType.CONFIG,
                "HP-15-FD1652TU-CONFIG", "HP 15-fd1652TU Config", 1, false),
            SeedProfile("73a3f07d-c906-4e49-a3db-4854ab1a9d08", LabelType.CO,
                "HP-15-FD1652TU-CO", "HP 15-fd1652TU CO", 2, true),
            SeedProfile("73a3f07d-c906-4e49-a3db-4854ab1a9d09", LabelType.CONFIG,
                "HP-15-FD1652TU-CONFIG", "HP 15-fd1652TU Config", 2, true));
    }

    private static LabelProfile SeedProfile(
        string id,
        LabelType type,
        string code,
        string name,
        int version,
        bool isActive) => new()
    {
        Id = Guid.Parse(id),
        BrandId = Guid.Parse("73a3f07d-c906-4e49-a3db-4854ab1a9d01"),
        ModelId = Guid.Parse("73a3f07d-c906-4e49-a3db-4854ab1a9d05"),
        LabelType = type,
        Code = code,
        Name = name,
        Version = version,
        IsActive = isActive,
        CreatedAtUtc = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
        EffectiveFromUtc = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero)
    };
}
