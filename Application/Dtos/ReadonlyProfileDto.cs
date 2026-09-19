namespace Application.Dtos;

public sealed record ReadonlyProfileDto(Guid Id, DateTime CreatedAt, DateTime UpdatedAt, IReadOnlyList<ProfileAttributeDto> Attributes, IReadOnlyList<ProjectDto> Projects, IReadOnlyList<CVDto> CVs);
