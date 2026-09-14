using Domain.Enums;
using Domain.Value_Objects;

namespace Application.Dtos;

public record AttributeValueDto(
        Guid AttributeId,
        int Order,
        string? StringValue,
        double? NumericValue,
        DateOnly? DateValue,
        Period? PeriodValue,
        bool? CheckboxValue,
        Guid? DropdownOptionId)
{
    public object? GetValue(AttributeType type) =>
        type switch
        {
            AttributeType.String
             or
            AttributeType.Text
             or
            AttributeType.Image => StringValue,
            AttributeType.Numeric => NumericValue,
            AttributeType.Date => DateValue,
            AttributeType.Period => PeriodValue,
            AttributeType.Checkbox => CheckboxValue,
            AttributeType.Dropdown => DropdownOptionId,

            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };
};

