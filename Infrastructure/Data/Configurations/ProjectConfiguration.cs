using Domain.Entities;
using Domain.Value_Objects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("Projects");
        builder.ConfigureBaseEntity();

        builder.Property(project => project.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(project => project.Description)
            .IsRequired();

        builder.HasOne(project => project.Profile)
            .WithMany(profile => profile.Projects)
            .HasForeignKey(project => project.ProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.OwnsOne(project => project.Period, period =>
        {
            period.Property(item => item.Start)
                .HasColumnName("PeriodStart")
                .IsRequired();

            period.Property(item => item.End)
                .HasColumnName("PeriodEnd");
        });

        builder.Navigation(project => project.Period)
            .IsRequired();

        builder.HasMany(project => project.Tags)
            .WithMany()
            .UsingEntity<Dictionary<string, object>>(
                "ProjectTags",
                right => right
                    .HasOne<Tag>()
                    .WithMany()
                    .HasForeignKey("TagName")
                    .OnDelete(DeleteBehavior.Cascade),
                left => left
                    .HasOne<Project>()
                    .WithMany()
                    .HasForeignKey("ProjectId")
                    .OnDelete(DeleteBehavior.Cascade),
                join =>
                {
                    join.ToTable("ProjectTags");
                    join.HasKey("ProjectId", "TagName");
                });

        builder.HasIndex(project => project.ProfileId);

        builder.Navigation(project => project.Tags)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
