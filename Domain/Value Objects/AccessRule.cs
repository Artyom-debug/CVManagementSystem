using Domain.Abstractions;
using Domain.Enums;

namespace Domain.Value_Objects;

public sealed class AccessRule : ValueObject
{
    public Guid AttributeId { get; private set; }

    public AttributeType AttributeType { get; private set; }

    public Operator Operator { get; private set; }

    public AccessRuleValue Value { get; private set; } = null!;

    private AccessRule()
    {
    }

    private AccessRule(Guid attributeId, AttributeType attributeType, Operator op)
    {
        if (attributeId == Guid.Empty)
            throw new ArgumentException("Attribute id cannot be empty", nameof(attributeId));
        if (!Enum.IsDefined(attributeType))
            throw new ArgumentOutOfRangeException(nameof(attributeType), attributeType, "Unknown attribute type");
        if (!Enum.IsDefined(op))
            throw new ArgumentOutOfRangeException(nameof(op), op, "Unknown access rule operator");

        ValidateOperator(attributeType, op);

        AttributeId = attributeId;
        AttributeType = attributeType;
        Operator = op;
    }

    public static AccessRule ForString(
        Guid attributeId,
        AttributeType attributeType,
        Operator op,
        string value)
    {
        if (attributeType is not (
            AttributeType.String or
            AttributeType.Text or
            AttributeType.Image))
        {
            throw new ArgumentException(
                "String rule can be created only for string, text or image attributes",
                nameof(attributeType));
        }
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Rule value cannot be empty", nameof(value));

        return new AccessRule(attributeId, attributeType, op)
        {
            Value = AccessRuleValue.FromString(value)
        };
    }

    public static AccessRule ForNumeric(
        Guid attributeId,
        Operator op,
        double value)
    {
        return new AccessRule(attributeId, AttributeType.Numeric, op)
        {
            Value = AccessRuleValue.FromNumeric(value)
        };
    }

    public static AccessRule ForDate(
        Guid attributeId,
        Operator op,
        DateOnly value)
    {
        return new AccessRule(attributeId, AttributeType.Date, op)
        {
            Value = AccessRuleValue.FromDate(value)
        };
    }

    public static AccessRule ForPeriod(
        Guid attributeId,
        Operator op,
        Period value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return new AccessRule(attributeId, AttributeType.Period, op)
        {
            Value = AccessRuleValue.FromPeriod(value)
        };
    }

    public static AccessRule ForBoolean(
        Guid attributeId,
        Operator op,
        bool value)
    {
        return new AccessRule(attributeId, AttributeType.Checkbox, op)
        {
            Value = AccessRuleValue.FromBoolean(value)
        };
    }

    public static AccessRule ForDropdown(
        Guid attributeId,
        Operator op,
        Guid optionId)
    {
        if (optionId == Guid.Empty)
            throw new ArgumentException("Dropdown option id cannot be empty", nameof(optionId));

        return new AccessRule(attributeId, AttributeType.Dropdown, op)
        {
            Value = AccessRuleValue.FromDropdown(optionId)
        };
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return AttributeId;
        yield return AttributeType;
        yield return Operator;
        yield return Value;
    }

    private static void ValidateOperator(AttributeType attributeType, Operator op)
    {
        var supportsOrdering = attributeType is AttributeType.Numeric or AttributeType.Date;
        var isEqualityOperator = op is Operator.Equal or Operator.NotEqual;

        if (!supportsOrdering && !isEqualityOperator)
        {
            throw new ArgumentException(
                $"Operator {op} is not supported for attribute type {attributeType}",
                nameof(op));
        }
    }
}
