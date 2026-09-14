using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public sealed class PositionAttributeConfiguration : IEntityTypeConfiguration<PositionAttribute>
{
    public void Configure(EntityTypeBuilder<PositionAttribute> builder)
    {
        builder.ToTable("PositionAttributes", table =>
        {
            table.HasCheckConstraint(
                "CK_PositionAttributes_DisplayOrder_NonNegative",
                "\"DisplayOrder\" >= 0");
        });

        builder.ConfigureBaseEntity();

        builder.Property(positionAttribute => positionAttribute.DisplayOrder)
            .IsRequired();

        builder.HasOne(positionAttribute => positionAttribute.Position)
            .WithMany(position => position.PositionAttributes)
            .HasForeignKey(positionAttribute => positionAttribute.PositionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(positionAttribute => positionAttribute.Attribute)
            .WithMany()
            .HasForeignKey(positionAttribute => positionAttribute.AttributeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(positionAttribute => new
        {
            positionAttribute.PositionId,
            positionAttribute.AttributeId
        }).IsUnique();

        builder.HasIndex(positionAttribute => new
        {
            positionAttribute.PositionId,
            positionAttribute.DisplayOrder
        }).IsUnique();
    }
}
