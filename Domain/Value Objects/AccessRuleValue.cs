using Domain.Abstractions;

namespace Domain.Value_Objects;

public sealed class AccessRuleValue : ValueObject
{
    public string? StringValue { get; }

    public double? NumericValue { get; }

    public DateOnly? DateValue { get; }

    public DateOnly? PeriodStart { get; }

    public DateOnly? PeriodEnd { get; }

    public bool? BooleanValue { get; }

    public Guid? DropdownOptionId { get; }

    private AccessRuleValue(
        string? stringValue = null,
        double? numericValue = null,
        DateOnly? dateValue = null,
        DateOnly? periodStart = null,
        DateOnly? periodEnd = null,
        bool? booleanValue = null,
        Guid? dropdownOptionId = null)
    {
        StringValue = stringValue;
        NumericValue = numericValue;
        DateValue = dateValue;
        PeriodStart = periodStart;
        PeriodEnd = periodEnd;
        BooleanValue = booleanValue;
        DropdownOptionId = dropdownOptionId;
    }

    public static AccessRuleValue FromString(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Rule value cannot be empty", nameof(value));

        return new AccessRuleValue(stringValue: value);
    }

    public static AccessRuleValue FromNumeric(double value) =>
        new(numericValue: value);

    public static AccessRuleValue FromDate(DateOnly value) =>
        new(dateValue: value);

    public static AccessRuleValue FromPeriod(Period value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return new AccessRuleValue(
            periodStart: value.Start,
            periodEnd: value.End);
    }

    public static AccessRuleValue FromBoolean(bool value) =>
        new(booleanValue: value);

    public static AccessRuleValue FromDropdown(Guid optionId)
    {
        if (optionId == Guid.Empty)
            throw new ArgumentException("Dropdown option id cannot be empty", nameof(optionId));

        return new AccessRuleValue(dropdownOptionId: optionId);
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
