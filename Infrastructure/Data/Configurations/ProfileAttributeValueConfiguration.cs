using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public sealed class ProfileAttributeValueConfiguration : IEntityTypeConfiguration<ProfileAttributeValue>
{
    public void Configure(EntityTypeBuilder<ProfileAttributeValue> builder)
    {
        builder.ToTable("ProfileAttributeValues");
        builder.ConfigureBaseEntity();

        builder.Property(value => value.StringValue)
            .HasMaxLength(500);

        builder.Property(value => value.ImageValue)
            .HasMaxLength(2000);

        builder.HasOne(value => value.Profile)
            .WithMany(profile => profile.AttributeValues)
            .HasForeignKey(value => value.ProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(value => value.Attribute)
            .WithMany()
            .HasForeignKey(value => value.AttributeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(value => value.DropdownOption)
            .WithMany()
            .HasForeignKey(value => value.DropdownOptionId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.OwnsOne(value => value.PeriodValue, period =>
        {
            period.Property(item => item.Start)
                .HasColumnName("PeriodStart");

            period.Property(item => item.End)
                .HasColumnName("PeriodEnd");
        });

        builder.HasIndex(value => new
        {
            value.ProfileId,
            value.AttributeId
        }).IsUnique();

        builder.HasIndex(value => value.DropdownOptionId);
    }
}
