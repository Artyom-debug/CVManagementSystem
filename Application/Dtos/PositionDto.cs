using Domain.Enums;

namespace Application.Dtos;

public sealed record PositionDto(
    Guid Id,
    int Version,
    string Name,
    string? Description,
    int MaxProjectCount,
    bool IsPublic,
    IReadOnlyList<string> Tags);

public sealed record DetailedPositionDto(
    Guid Id,
    int Version,
    string Name,
    string? Description,
    int MaxProjectCount,
    bool IsPublic,
    IReadOnlyList<string> Tags,
    IReadOnlyList<PositionAttributeDto> Attributes,
    IReadOnlyList<PositionAccessRuleDto> AccessRules,
    IReadOnlyList<DiscussionPostDto> Discussion);

public sealed record PositionAttributeDto(
    int DisplayOrder,
    DetailedAttributeDto Attribute);

public sealed record PositionAccessRuleDto(
    Guid AttributeId,
    AttributeType AttributeType,
    Operator Operator,
    string? StringValue,
    double? NumericValue,
    DateOnly? DateValue,
    DateOnly? PeriodStart,
    DateOnly? PeriodEnd,
    bool? BooleanValue,
    Guid? DropdownOptionId);

public sealed record DiscussionPostDto(
    Guid Id,
    string AuthorId,
    string Content,
    DateTime CreatedAt);
