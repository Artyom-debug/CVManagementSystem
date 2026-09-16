using Domain.Enums;
using Domain.Value_Objects;

namespace Application.Commands.Position;

public sealed record PositionAttributeInput(
    Guid AttributeId,
    int DisplayOrder);

public sealed record PositionAccessRuleInput(
    Guid AttributeId,
    Operator Operator,
    string? StringValue = null,
    double? NumericValue = null,
    DateOnly? DateValue = null,
    DateOnly? PeriodStart = null,
    DateOnly? PeriodEnd = null,
    bool? BooleanValue = null,
    Guid? DropdownOptionId = null);

internal static class PositionCommandModels
{
    public static AccessRule CloneAccessRule(AccessRule rule)
    {
        return rule.AttributeType switch
        {
            AttributeType.String or AttributeType.Text or AttributeType.Image =>
                AccessRule.ForString(
                    rule.AttributeId,
                    rule.AttributeType,
                    rule.Operator,
                    rule.Value.StringValue!),

            AttributeType.Numeric => AccessRule.ForNumeric(
                rule.AttributeId,
                rule.Operator,
                rule.Value.NumericValue!.Value),

            AttributeType.Date => AccessRule.ForDate(
                rule.AttributeId,
                rule.Operator,
                rule.Value.DateValue!.Value),

            AttributeType.Period => AccessRule.ForPeriod(
                rule.AttributeId,
                rule.Operator,
                rule.Value.PeriodEnd.HasValue
                    ? new Period(rule.Value.PeriodStart!.Value, rule.Value.PeriodEnd.Value)
                    : new Period(rule.Value.PeriodStart!.Value)),

            AttributeType.Checkbox => AccessRule.ForBoolean(
                rule.AttributeId,
                rule.Operator,
                rule.Value.BooleanValue!.Value),

            AttributeType.Dropdown => AccessRule.ForDropdown(
                rule.AttributeId,
                rule.Operator,
                rule.Value.DropdownOptionId!.Value),

            _ => throw new ArgumentOutOfRangeException(nameof(rule.AttributeType))
        };
    }

    public static bool TryCreateAccessRules(
        IReadOnlyCollection<PositionAccessRuleInput> inputs,
        IReadOnlyDictionary<Guid, Domain.Entities.Attribute> attributes,
        out IReadOnlyCollection<AccessRule> rules,
        out string? error)
    {
        var result = new List<AccessRule>();

        foreach (var input in inputs)
        {
            if (!attributes.TryGetValue(input.AttributeId, out var attribute))
            {
                rules = [];
                error = $"Attribute '{input.AttributeId}' was not found.";
                return false;
            }

            try
            {
                var rule = CreateAccessRule(input, attribute);
                result.Add(rule);
            }
            catch (ArgumentException exception)
            {
                rules = [];
                error = exception.Message;
                return false;
            }
        }

        rules = result;
        error = null;
        return true;
    }

    private static AccessRule CreateAccessRule(
        PositionAccessRuleInput input,
        Domain.Entities.Attribute attribute)
    {
        return attribute.Type switch
        {
            AttributeType.String or AttributeType.Text or AttributeType.Image =>
                AccessRule.ForString(
                    attribute.Id,
                    attribute.Type,
                    input.Operator,
                    input.StringValue
                        ?? throw new ArgumentException($"Value for attribute '{attribute.Name}' is required.")),

            AttributeType.Numeric => AccessRule.ForNumeric(
                attribute.Id,
                input.Operator,
                input.NumericValue
                    ?? throw new ArgumentException($"Value for attribute '{attribute.Name}' is required.")),

            AttributeType.Date => AccessRule.ForDate(
                attribute.Id,
                input.Operator,
                input.DateValue
                    ?? throw new ArgumentException($"Value for attribute '{attribute.Name}' is required.")),

            AttributeType.Period => AccessRule.ForPeriod(
                attribute.Id,
                input.Operator,
                CreatePeriod(input, attribute.Name)),

            AttributeType.Checkbox => AccessRule.ForBoolean(
                attribute.Id,
                input.Operator,
                input.BooleanValue
                    ?? throw new ArgumentException($"Value for attribute '{attribute.Name}' is required.")),

            AttributeType.Dropdown => AccessRule.ForDropdown(
                attribute.Id,
                input.Operator,
                GetDropdownOptionId(input, attribute)),

            _ => throw new ArgumentOutOfRangeException(nameof(attribute.Type))
        };
    }

    private static Period CreatePeriod(
        PositionAccessRuleInput input,
        string attributeName)
    {
        var start = input.PeriodStart
            ?? throw new ArgumentException($"Period start for attribute '{attributeName}' is required.");

        return input.PeriodEnd.HasValue
            ? new Period(start, input.PeriodEnd.Value)
            : new Period(start);
    }

    private static Guid GetDropdownOptionId(
        PositionAccessRuleInput input,
        Domain.Entities.Attribute attribute)
    {
        var optionId = input.DropdownOptionId
            ?? throw new ArgumentException($"Dropdown option for attribute '{attribute.Name}' is required.");

        if (attribute.Options.All(option => option.Id != optionId))
        {
            throw new ArgumentException(
                $"Selected option does not belong to attribute '{attribute.Name}'.");
        }

        return optionId;
    }
}
