using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public sealed class LikeConfiguration : IEntityTypeConfiguration<Like>
{
    public void Configure(EntityTypeBuilder<Like> builder)
    {
        builder.ToTable("Likes");
        builder.ConfigureBaseEntity();

        builder.Property(like => like.RecruterId)
            .HasMaxLength(450)
            .IsRequired();

        builder.HasOne<CV>()
            .WithMany(cv => cv.Likes)
            .HasForeignKey(like => like.CVId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(like => new
        {
            like.CVId,
            like.RecruterId
        }).IsUnique();
    }
}
