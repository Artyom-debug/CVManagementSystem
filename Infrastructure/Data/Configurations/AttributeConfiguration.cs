using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public sealed class AttributeConfiguration : IEntityTypeConfiguration<Domain.Entities.Attribute>
{
    public void Configure(EntityTypeBuilder<Domain.Entities.Attribute> builder)
    {
        builder.ToTable("Attributes");
        builder.ConfigureBaseEntity();

        builder.Property(attribute => attribute.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(attribute => attribute.Description)
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(attribute => attribute.Type)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(attribute => attribute.Category)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(attribute => attribute.IsSystem)
            .IsRequired();

        builder.HasIndex(attribute => attribute.Name, "IX_Attributes_Name_Unique")
            .IsUnique();

        builder.HasIndex(attribute => attribute.Name, "IX_Attributes_Name_Trigram")
            .HasMethod("gin")
            .HasOperators("gin_trgm_ops");

        builder.HasIndex(attribute => new
        {
            attribute.Category,
            attribute.Name
        });

        builder.Navigation(attribute => attribute.Options)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
