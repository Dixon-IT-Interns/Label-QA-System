using Dixon.LabelVerification.API.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dixon.LabelVerification.API.Data.Configurations;

public sealed class FieldAliasConfiguration : IEntityTypeConfiguration<FieldAlias>
{
    public void Configure(EntityTypeBuilder<FieldAlias> entity)
    {
        entity.ToTable("FieldAliases");
        entity.Property(x => x.Value).HasColumnName("AliasText").HasMaxLength(200).IsRequired();
        entity.Property(x => x.Language).HasMaxLength(20).HasDefaultValue("en").IsRequired();
        entity.Property(x => x.IsActive).HasDefaultValue(true);
        entity.HasIndex(x => new { x.FieldDefinitionId, x.Value, x.Language }).IsUnique();
        entity.HasOne(x => x.FieldDefinition).WithMany(x => x.Aliases)
            .HasForeignKey(x => x.FieldDefinitionId).OnDelete(DeleteBehavior.Restrict);
        entity.HasData(
            SeedAlias("73a3f07d-c906-4e49-a3db-4854ab1a9d30", "73a3f07d-c906-4e49-a3db-4854ab1a9d10", "Product Model"),
            SeedAlias("73a3f07d-c906-4e49-a3db-4854ab1a9d31", "73a3f07d-c906-4e49-a3db-4854ab1a9d10", "Model Number"),
            SeedAlias("73a3f07d-c906-4e49-a3db-4854ab1a9d32", "73a3f07d-c906-4e49-a3db-4854ab1a9d11", "CPU"),
            SeedAlias("73a3f07d-c906-4e49-a3db-4854ab1a9d33", "73a3f07d-c906-4e49-a3db-4854ab1a9d12", "SSD"),
            SeedAlias("73a3f07d-c906-4e49-a3db-4854ab1a9d34", "73a3f07d-c906-4e49-a3db-4854ab1a9d13", "RAM"),
            SeedAlias("73a3f07d-c906-4e49-a3db-4854ab1a9d35", "73a3f07d-c906-4e49-a3db-4854ab1a9d14", "Screen"),
            SeedAlias("73a3f07d-c906-4e49-a3db-4854ab1a9d36", "73a3f07d-c906-4e49-a3db-4854ab1a9d15", "Battery Capacity"),
            SeedAlias("73a3f07d-c906-4e49-a3db-4854ab1a9d37", "73a3f07d-c906-4e49-a3db-4854ab1a9d20", "P/N"),
            SeedAlias("73a3f07d-c906-4e49-a3db-4854ab1a9d38", "73a3f07d-c906-4e49-a3db-4854ab1a9d20", "Product Number"),
            SeedAlias("73a3f07d-c906-4e49-a3db-4854ab1a9d39", "73a3f07d-c906-4e49-a3db-4854ab1a9d21", "PRDN Order"),
            SeedAlias("73a3f07d-c906-4e49-a3db-4854ab1a9d3a", "73a3f07d-c906-4e49-a3db-4854ab1a9d21", "PRON Order"),
            SeedAlias("73a3f07d-c906-4e49-a3db-4854ab1a9d3b", "73a3f07d-c906-4e49-a3db-4854ab1a9d21", "Production Order"));
    }

    private static FieldAlias SeedAlias(string id, string fieldId, string value) => new()
    {
        Id = Guid.Parse(id),
        FieldDefinitionId = Guid.Parse(fieldId),
        Value = value,
        Language = "en",
        IsActive = true,
        CreatedAtUtc = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero)
    };
}
