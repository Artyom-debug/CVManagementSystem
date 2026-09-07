using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public sealed class AttributeConfiguration : IEntityTypeConfiguration<Domain.Entities.Attribute>
{
    public void Configure(EntityTypeBuilder<Domain.Entities.Attribute> builder)
    {
        builder.Property(attribute => attribute.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.HasIndex(attribute => attribute.Name)
            .IsUnique();
    }
}
