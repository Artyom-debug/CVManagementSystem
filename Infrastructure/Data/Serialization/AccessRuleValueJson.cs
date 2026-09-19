using Domain.Value_Objects;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Infrastructure.Data.Serialization;

internal static class AccessRuleValueJson
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static ValueConverter<AccessRuleValue, string> CreateConverter() =>
        new(value => Serialize(value), json => Deserialize(json));

    private static string Serialize(AccessRuleValue value)
    {
        var document = new AccessRuleValueDocument
        {
            String = value.StringValue,
            Number = value.NumericValue,
            Date = value.DateValue,
            Period = value.PeriodStart.HasValue
                ? new PeriodDocument(value.PeriodStart.Value, value.PeriodEnd)
                : null,
            Boolean = value.BooleanValue,
            OptionId = value.DropdownOptionId
        };

        return JsonSerializer.Serialize(document, SerializerOptions);
    }

    private static AccessRuleValue Deserialize(string json)
    {
        var document = JsonSerializer.Deserialize<AccessRuleValueDocument>(json, SerializerOptions)
            ?? throw new JsonException("Access rule value cannot be null");

        var populatedValues =
            (document.String is not null ? 1 : 0) +
            (document.Number.HasValue ? 1 : 0) +
            (document.Date.HasValue ? 1 : 0) +
            (document.Period is not null ? 1 : 0) +
            (document.Boolean.HasValue ? 1 : 0) +
            (document.OptionId.HasValue ? 1 : 0);

        if (populatedValues != 1)
            throw new JsonException("Access rule JSON must contain exactly one value");

        if (document.String is not null)
            return AccessRuleValue.FromString(document.String);
        if (document.Number.HasValue)
            return AccessRuleValue.FromNumeric(document.Number.Value);
        if (document.Date.HasValue)
            return AccessRuleValue.FromDate(document.Date.Value);
        if (document.Period is not null)
        {
            var period = document.Period.End.HasValue
                ? new Period(document.Period.Start, document.Period.End.Value)
                : new Period(document.Period.Start);

            return AccessRuleValue.FromPeriod(period);
        }
        if (document.Boolean.HasValue)
            return AccessRuleValue.FromBoolean(document.Boolean.Value);

        return AccessRuleValue.FromDropdown(document.OptionId!.Value);
    }

    private sealed class AccessRuleValueDocument
    {
        public string? String { get; init; }

        public double? Number { get; init; }

        public DateOnly? Date { get; init; }

        public PeriodDocument? Period { get; init; }

        public bool? Boolean { get; init; }

        public Guid? OptionId { get; init; }
    }

    private sealed record PeriodDocument(DateOnly Start, DateOnly? End);
}
