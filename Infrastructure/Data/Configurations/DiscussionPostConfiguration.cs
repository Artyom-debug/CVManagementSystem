using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public sealed class DiscussionPostConfiguration : IEntityTypeConfiguration<DiscussionPost>
{
    public void Configure(EntityTypeBuilder<DiscussionPost> builder)
    {
        builder.ToTable("DiscussionPosts");
        builder.ConfigureBaseEntity();

        builder.Property(post => post.AuthorId)
            .HasMaxLength(450)
            .IsRequired();

        builder.Property(post => post.Content)
            .IsRequired();

        builder.Property(post => post.CreatedAt)
            .IsRequired();

        builder.HasOne(post => post.Position)
            .WithMany(position => position.DiscussionPosts)
            .HasForeignKey(post => post.PositionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(post => new
        {
            post.PositionId,
            post.CreatedAt
        });
    }
}
