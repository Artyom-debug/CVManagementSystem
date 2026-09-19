using Domain.Entities;
using Domain.Value_Objects;
using Infrastructure.Data.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public sealed class PositionConfiguration : IEntityTypeConfiguration<Position>
{
    public void Configure(EntityTypeBuilder<Position> builder)
    {
        builder.ToTable("Positions", table =>
        {
            table.HasCheckConstraint("CK_Positions_MaxProjectCount_NonNegative", "\"MaxProjectCount\" >= 0");
        });

        builder.ConfigureBaseEntity();

        builder.Property(position => position.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(position => position.Description)
            .HasMaxLength(2000);

        builder.Property(position => position.MaxProjectCount)
            .IsRequired();

        builder.Property(position => position.IsPublic)
            .IsRequired();

        builder.HasMany(position => position.Tags)
            .WithMany()
            .UsingEntity<Dictionary<string, object>>(
                "PositionTags",
                right => right
                    .HasOne<Tag>()
                    .WithMany()
                    .HasForeignKey("TagName")
                    .OnDelete(DeleteBehavior.Cascade),
                left => left
                    .HasOne<Position>()
                    .WithMany()
                    .HasForeignKey("PositionId")
                    .OnDelete(DeleteBehavior.Cascade),
                join =>
                {
                    join.ToTable("PositionTags");
                    join.HasKey("PositionId", "TagName");
                });

        builder.OwnsMany(position => position.AccessRules, rules =>
        {
            rules.ToTable("PositionAccessRules");
            rules.WithOwner().HasForeignKey("PositionId");

            rules.Property<Guid>("Id")
                .ValueGeneratedOnAdd();
            rules.HasKey("PositionId", "Id");

            rules.Property(rule => rule.AttributeId)
                .IsRequired();

            rules.Property(rule => rule.AttributeType)
                .HasConversion<string>()
                .HasMaxLength(30)
                .IsRequired();

            rules.Property(rule => rule.Operator)
                .HasConversion<string>()
                .HasMaxLength(30)
                .IsRequired();

            var valueProperty = rules.Property(rule => rule.Value)
                .HasConversion(AccessRuleValueJson.CreateConverter())
                .HasColumnType("jsonb")
                .HasColumnName("Value")
                .IsRequired();

            valueProperty.Metadata.SetValueComparer(new ValueComparer<AccessRuleValue>((left, right) => left == right, value => value.GetHashCode(), value => value));

            rules.HasOne<Domain.Entities.Attribute>()
                .WithMany()
                .HasForeignKey(rule => rule.AttributeId)
                .OnDelete(DeleteBehavior.Cascade);

            rules.HasIndex("PositionId", nameof(Domain.Value_Objects.AccessRule.AttributeId));
        });

        builder.Navigation(position => position.Tags)
            .HasField("_tags")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Navigation(position => position.PositionAttributes)
            .HasField("_positionAttributes")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Navigation(position => position.AccessRules)
            .HasField("_accessRules")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Navigation(position => position.CVs)
            .HasField("_cvs")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Navigation(position => position.DiscussionPosts)
            .HasField("_discussionPosts")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
