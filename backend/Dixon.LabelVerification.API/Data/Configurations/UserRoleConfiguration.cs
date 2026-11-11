using Dixon.LabelVerification.API.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dixon.LabelVerification.API.Data.Configurations;

public sealed class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> entity)
    {
        entity.ToTable("UserRoles");
        entity.HasKey(x => x.Code);
        entity.Property(x => x.Code).HasMaxLength(20).IsRequired();
        entity.Property(x => x.Name).HasMaxLength(50).IsRequired();
        entity.HasData(
            new UserRole { Code = "Admin", Name = "Administrator" },
            new UserRole { Code = "Operator", Name = "Operator" },
            new UserRole { Code = "Viewer", Name = "Viewer" });
    }
}
