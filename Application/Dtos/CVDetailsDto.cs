using Domain.Enums;

namespace Application.Dtos;

public sealed record CVDetailsDto(Guid Id, int Version, Guid ProfileId, int ProfileVersion, Guid PositionId, string PositionName, string? PositionDescription, Status Status, DateTime CreatedAt, DateTime? LastUpdated, DateTime? PublishedAt, IReadOnlyList<CVAttributeDto> Attributes, IReadOnlyList<ProjectDto> Projects, int LikesCount, bool IsLikedByCurrentUser);

public sealed record CVAttributeDto(int DisplayOrder, DetailedAttributeDto Attribute, AttributeValueDto? Value);
