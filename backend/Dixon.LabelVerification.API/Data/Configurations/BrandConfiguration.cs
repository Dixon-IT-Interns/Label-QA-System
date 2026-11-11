using Dixon.LabelVerification.API.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dixon.LabelVerification.API.Data.Configurations;

public sealed class BrandConfiguration : IEntityTypeConfiguration<Brand>
{
    public void Configure(EntityTypeBuilder<Brand> entity)
    {
        entity.ToTable("Brands");
        entity.Property(x => x.Code).HasMaxLength(50).IsRequired();
        entity.Property(x => x.Name).HasMaxLength(150).IsRequired();
        entity.Property(x => x.Description).HasMaxLength(500);
        entity.HasIndex(x => x.Code).IsUnique();
        entity.HasIndex(x => x.Name).IsUnique();
        entity.HasData(
            Seed("73a3f07d-c906-4e49-a3db-4854ab1a9d01", "HP", "HP"),
            Seed("73a3f07d-c906-4e49-a3db-4854ab1a9d02", "ASUS", "ASUS"),
            Seed("73a3f07d-c906-4e49-a3db-4854ab1a9d03", "ACER", "Acer"),
            Seed("73a3f07d-c906-4e49-a3db-4854ab1a9d04", "GIGABYTE", "Gigabyte"));
    }

    private static Brand Seed(string id, string code, string name) => new()
    {
        Id = Guid.Parse(id),
        Code = code,
        Name = name,
        CreatedAtUtc = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero)
    };
}
