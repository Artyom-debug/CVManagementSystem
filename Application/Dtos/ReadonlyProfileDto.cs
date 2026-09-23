namespace Application.Dtos;

public sealed record ReadonlyProfileDto(Guid Id, int Version, DateTime CreatedAt, DateTime UpdatedAt, IReadOnlyList<ProfileAttributeDto> Attributes, IReadOnlyList<ProjectDto> Projects, IReadOnlyList<CVDto> CVs);
