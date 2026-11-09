using BuildingBlocks.Data;
using Identity.Domain;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Persistence;

/// <summary>The Identity module's slice of the shared EF model: owns the <c>identity</c> schema.</summary>
public sealed class IdentityModelConfiguration : IModuleModelConfiguration
{
    public const string Schema = "identity";

    public void Configure(ModelBuilder modelBuilder) =>
        modelBuilder.Entity<User>(user =>
        {
            user.ToTable("users", Schema);
            user.HasKey(u => u.Id);
            user.Property(u => u.Id).ValueGeneratedNever();
            user.Property(u => u.Email).HasMaxLength(254).IsRequired();
            user.HasIndex(u => u.Email).IsUnique();
            user.Property(u => u.DisplayName).HasMaxLength(100).IsRequired();
            user.Property(u => u.PasswordHash).HasMaxLength(512).IsRequired();
            user.Property(u => u.Role).HasConversion<string>().HasMaxLength(20).IsRequired();
            user.Ignore(u => u.Permissions);
        });
}
