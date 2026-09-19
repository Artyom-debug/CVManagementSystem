using Domain.Enums;

namespace Application.Dtos;

public sealed record CVDto(Guid Id, Guid PositionId, string PositionName, Status Status, DateTime CreatedAt, DateTime? LastUpdated, DateTime? PublishedAt);
