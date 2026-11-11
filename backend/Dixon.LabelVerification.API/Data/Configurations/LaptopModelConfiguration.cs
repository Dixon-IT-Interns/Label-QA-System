using Dixon.LabelVerification.API.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dixon.LabelVerification.API.Data.Configurations;

public sealed class LaptopModelConfiguration : IEntityTypeConfiguration<LaptopModel>
{
    public void Configure(EntityTypeBuilder<LaptopModel> entity)
    {
        entity.ToTable("LaptopModels");
        entity.Property(x => x.Code).HasColumnName("ModelNumber").HasMaxLength(100).IsRequired();
        entity.Property(x => x.Name).HasColumnName("ModelName").HasMaxLength(200).IsRequired();
        entity.Property(x => x.Description).HasMaxLength(500);
        entity.HasIndex(x => new { x.BrandId, x.Code }).IsUnique();
        entity.HasAlternateKey(x => new { x.BrandId, x.Id });
        entity.HasOne(x => x.Brand).WithMany(x => x.Models)
            .HasForeignKey(x => x.BrandId).OnDelete(DeleteBehavior.Restrict);
        entity.HasData(new LaptopModel
        {
            Id = Guid.Parse("73a3f07d-c906-4e49-a3db-4854ab1a9d05"),
            BrandId = Guid.Parse("73a3f07d-c906-4e49-a3db-4854ab1a9d01"),
            Code = "15-fd1652TU",
            Name = "15-fd1652TU",
            CreatedAtUtc = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero)
        });
    }
}
