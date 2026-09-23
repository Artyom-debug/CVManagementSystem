using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public sealed class CVConfiguration : IEntityTypeConfiguration<CV>
{
    public void Configure(EntityTypeBuilder<CV> builder)
    {
        builder.ToTable("CVs", table =>
        {
            table.HasCheckConstraint("CK_CVs_Status", "\"Status\" IN ('Draft', 'Published')");
        });
        builder.ConfigureBaseEntity();

        builder.Property(cv => cv.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(cv => cv.CreatedAt)
            .IsRequired();

        builder.Property(cv => cv.IsRemovedFromProfile)
            .IsRequired();

        builder.HasOne(cv => cv.Profile)
            .WithMany()
            .HasForeignKey(cv => cv.ProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(cv => cv.Position)
            .WithMany(position => position.CVs)
            .HasForeignKey(cv => cv.PositionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(cv => new
        {
            cv.ProfileId,
            cv.PositionId
        }).IsUnique();

        builder.HasIndex(cv => new
        {
            cv.PositionId,
            cv.Status,
            cv.CreatedAt
        });

        builder.Navigation(cv => cv.Likes)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
