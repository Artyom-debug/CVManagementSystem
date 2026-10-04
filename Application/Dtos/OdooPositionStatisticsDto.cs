namespace Application.Dtos;

public sealed record OdooTopValueDto(string Value, int Count);

public sealed record OdooAttributeStatisticDto(
    Guid AttributeId,
    string Name,
    string Type,
    int DisplayOrder,
    int FilledCount,
    double? NumericMinimum,
    double? NumericMaximum,
    double? NumericAverage,
    DateOnly? EarliestDate,
    DateOnly? LatestDate,
    DateOnly? EarliestPeriodStart,
    DateOnly? LatestPeriodEnd,
    int TrueCount,
    int FalseCount,
    IReadOnlyList<OdooTopValueDto> TopValues);

public sealed record OdooPositionStatisticsDto(
    Guid PositionId,
    string Name,
    string? Description,
    DateTime CreatedAt,
    int PublishedCvCount,
    IReadOnlyList<OdooAttributeStatisticDto> Attributes);
