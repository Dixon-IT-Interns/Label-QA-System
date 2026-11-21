using Dixon.LabelVerification.API.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dixon.LabelVerification.API.Data.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> entity)
    {
        entity.ToTable("ApplicationUsers", table =>
            table.HasCheckConstraint(
                "CK_ApplicationUsers_Role",
                "[Role] IN ('Admin', 'Supervisor', 'Operator')"));
        entity.Property(x => x.FullName).HasMaxLength(150).IsRequired();
        entity.Property(x => x.Username).HasMaxLength(100).IsRequired();
        entity.Property(x => x.Email).HasMaxLength(256).IsRequired();
        entity.Property(x => x.PasswordHash).HasMaxLength(512).IsRequired();
        entity.Property(x => x.Role).HasMaxLength(20).IsRequired();
        entity.Property(x => x.PasswordResetTokenHash).HasMaxLength(64);
        entity.HasIndex(x => x.Username).IsUnique();
        entity.HasIndex(x => x.Email).IsUnique();
        entity.HasIndex(x => x.BrandId);
        entity.HasOne(x => x.UserRole).WithMany(x => x.Users)
            .HasForeignKey(x => x.Role).HasPrincipalKey(x => x.Code)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.Brand).WithMany(x => x.Users)
            .HasForeignKey(x => x.BrandId).OnDelete(DeleteBehavior.Restrict);
    }
}
