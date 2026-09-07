using Domain.Abstractions;
using Domain.Value_Objects;
using Domain.Enums;

namespace Domain.Entities;

public sealed class ProfileAttributeValue : BaseEntity
{
    public string? StringValue { get; private set; } = null;

    public string? TextValue { get; private set; } = null;

    public string? ImageValue { get; private set; } = null;

    public double? NumericValue { get; private set; } = null;

    public DateOnly? DateValue { get; private set; } = null;

    public Period? PeriodValue { get; private set; } = null;

    public bool? CheckboxValue { get; private set; } = null;

    public Guid? DropdownOptionId { get; private set; }

    public AttributeOptions? DropdownOption { get; private set; }

    public Guid ProfileId { get; private set; }

    public Profile? Profile { get; private set; }

    public Guid AttributeId { get; private set; }

    public Attribute? Attribute { get; private set; }

    public ProfileAttributeValue(Guid profileId, Guid attributeId)
    {
        if (profileId == Guid.Empty)
            throw new ArgumentException("Profile id cannot be empty", nameof(profileId));
        if (attributeId == Guid.Empty)
            throw new ArgumentException("Attribute id cannot be empty", nameof(attributeId));

        ProfileId = profileId;
        AttributeId = attributeId;
    }

    public void UpdateAttributeValue(object? value, AttributeType type)
    {
        if (!Enum.IsDefined(type))
            throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown attribute type");

        string? stringValue = null;
        string? textValue = null;
        string? imageValue = null;
        double? numericValue = null;
        DateOnly? dateValue = null;
        Period? periodValue = null;
        bool? checkboxValue = null;
        Guid? dropdownOptionId = null;

        switch (type)
        {
            case AttributeType.String:
                stringValue = GetStringValue(value, type);
                break;
            case AttributeType.Text:
                textValue = GetStringValue(value, type);
                break;
            case AttributeType.Image:
                imageValue = GetStringValue(value, type);
                break;
            case AttributeType.Numeric:
                numericValue = GetNumericValue(value);
                break;
            case AttributeType.Date:
                dateValue = GetStructValue<DateOnly>(value, type);
                break;
            case AttributeType.Period:
                periodValue = GetReferenceValue<Period>(value, type);
                break;
            case AttributeType.Checkbox:
                checkboxValue = GetBooleanValue(value);
                break;
            case AttributeType.Dropdown:
                dropdownOptionId = GetStructValue<Guid>(value, type);
                if (dropdownOptionId == Guid.Empty)
                    throw new ArgumentException("Dropdown option id cannot be empty", nameof(value));
                break;
        }

        StringValue = stringValue;
        TextValue = textValue;
        ImageValue = imageValue;
        NumericValue = numericValue;
        DateValue = dateValue;
        PeriodValue = periodValue;
        CheckboxValue = checkboxValue;
        DropdownOptionId = dropdownOptionId;
    }

    public object? GetAttributeValue(AttributeType type)
    {
        switch (type)
        {
            case AttributeType.String:
                return StringValue;
            case AttributeType.Text:
                return TextValue;
            case AttributeType.Image:
                return ImageValue;
            case AttributeType.Numeric:
                return NumericValue;
            case AttributeType.Date:
                return DateValue;
            case AttributeType.Period:
                return PeriodValue;
            case AttributeType.Checkbox:
                return CheckboxValue;
            case AttributeType.Dropdown:
                return DropdownOptionId;
            default:
                throw new NotSupportedException($"DataType {type} not supported");
        }
    }

    private static string? GetStringValue(object? value, AttributeType type)
    {
        if (value is null)
            return null;
        if (value is string stringValue)
            return stringValue;

        throw new ArgumentException($"Value for attribute type {type} must be a string", nameof(value));
    }

    private static double? GetNumericValue(object? value)
    {
        if (value is null)
            return null;

        return value switch
        {
            int number => number,
            double number => number,
            _ => throw new ArgumentException("Numeric attribute value must be a numeric type", nameof(value))
        };
    }

    private static bool? GetBooleanValue(object? value)
    {
        if (value is null)
            return null;
        if (value is bool resValue)
            return resValue;
        throw new ArgumentException($"Boolean attribute value must be a boolean type", nameof(value));
    }

    private static T? GetStructValue<T>(object? value, AttributeType type)
        where T : struct
    {
        if (value is null)
            return null;
        if (value is T typedValue)
            return typedValue;
        throw new ArgumentException($"Value for attribute type {type} must be {typeof(T).Name}", nameof(value));
    }

    private static T? GetReferenceValue<T>(object? value, AttributeType type)
        where T : class
    {
        if (value is null)
            return null;
        if (value is T typedValue)
            return typedValue;
        throw new ArgumentException($"Value for attribute type {type} must be {typeof(T).Name}", nameof(value));
    }
}
