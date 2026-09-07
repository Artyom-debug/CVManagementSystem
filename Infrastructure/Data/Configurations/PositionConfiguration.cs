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
                .IsRequired();

            rules.Property(rule => rule.Operator)
                .IsRequired();

            rules.Ignore(rule => rule.StringValue);
            rules.Ignore(rule => rule.NumericValue);
            rules.Ignore(rule => rule.DateValue);
            rules.Ignore(rule => rule.PeriodStart);
            rules.Ignore(rule => rule.PeriodEnd);
            rules.Ignore(rule => rule.BooleanValue);
            rules.Ignore(rule => rule.DropdownOptionId);

            var valueProperty = rules.Property(rule => rule.Value)
                .HasConversion(AccessRuleValueJson.CreateConverter())
                .HasColumnType("jsonb")
                .HasColumnName("Value")
                .IsRequired();

            valueProperty.Metadata.SetValueComparer(
                new ValueComparer<AccessRuleValue>(
                    (left, right) => left == right,
                    value => value.GetHashCode(),
                    value => value));

            rules.HasIndex("PositionId", nameof(Domain.Value_Objects.AccessRule.AttributeId));
        });
    }
}
