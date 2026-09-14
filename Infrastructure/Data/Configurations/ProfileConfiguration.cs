using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public sealed class ProfileConfiguration : IEntityTypeConfiguration<Profile>
{
    public void Configure(EntityTypeBuilder<Profile> builder)
    {
        builder.ToTable("Profiles");
        builder.ConfigureBaseEntity();

        builder.Property(profile => profile.UserId)
            .HasMaxLength(450)
            .IsRequired();

        builder.Property(profile => profile.CreatedAt)
            .IsRequired();

        builder.Property(profile => profile.UpdatedAt)
            .IsRequired();

        builder.HasIndex(profile => profile.UserId)
            .IsUnique();

        builder.Navigation(profile => profile.AttributeValues)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Navigation(profile => profile.Projects)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
