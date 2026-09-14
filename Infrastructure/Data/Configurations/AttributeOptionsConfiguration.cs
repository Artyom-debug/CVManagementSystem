using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public sealed class AttributeOptionsConfiguration : IEntityTypeConfiguration<AttributeOptions>
{
    public void Configure(EntityTypeBuilder<AttributeOptions> builder)
    {
        builder.ToTable("AttributeOptions");
        builder.ConfigureBaseEntity();

        builder.Property(option => option.Option)
            .HasMaxLength(200)
            .IsRequired();

        builder.HasOne(option => option.Attribute)
            .WithMany(attribute => attribute.Options)
            .HasForeignKey(option => option.AttributeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(option => new
        {
            option.AttributeId,
            option.Option
        }).IsUnique();
    }
}
