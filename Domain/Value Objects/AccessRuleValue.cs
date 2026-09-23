using Domain.Abstractions;

namespace Domain.Value_Objects;

public sealed class AccessRuleValue : ValueObject
{
    public string? StringValue { get; private set; }

    public double? NumericValue { get; private set; }

    public DateOnly? DateValue { get; private set; }

    public DateOnly? PeriodStart { get; private set; }

    public DateOnly? PeriodEnd { get; private set; }

    public bool? BooleanValue { get; private set; }

    public Guid? DropdownOptionId { get; private set; }

    private AccessRuleValue()
    {
    }

    public static AccessRuleValue FromString(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Rule value cannot be empty", nameof(value));

        return new AccessRuleValue { StringValue = value };
    }

    public static AccessRuleValue FromNumeric(double value) =>
        new() { NumericValue = value };

    public static AccessRuleValue FromDate(DateOnly value) =>
        new() { DateValue = value };

    public static AccessRuleValue FromPeriod(Period value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return new AccessRuleValue
        {
            PeriodStart = value.Start,
            PeriodEnd = value.End
        };
    }

    public static AccessRuleValue FromBoolean(bool value) =>
        new() { BooleanValue = value };

    public static AccessRuleValue FromDropdown(Guid optionId)
    {
        if (optionId == Guid.Empty)
            throw new ArgumentException("Dropdown option id cannot be empty", nameof(optionId));

        return new AccessRuleValue { DropdownOptionId = optionId };
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return StringValue ?? string.Empty;
        yield return NumericValue.HasValue;
        if (NumericValue.HasValue)
            yield return NumericValue.Value;
        yield return DateValue.HasValue;
        if (DateValue.HasValue)
            yield return DateValue.Value;
        yield return PeriodStart.HasValue;
        if (PeriodStart.HasValue)
            yield return PeriodStart.Value;
        yield return PeriodEnd.HasValue;
        if (PeriodEnd.HasValue)
            yield return PeriodEnd.Value;
        yield return BooleanValue.HasValue;
        if (BooleanValue.HasValue)
            yield return BooleanValue.Value;
        yield return DropdownOptionId.HasValue;
        if (DropdownOptionId.HasValue)
            yield return DropdownOptionId.Value;
    }
}
