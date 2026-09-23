namespace Application.Dtos;

public sealed record IdentityUserDto(string Id, Guid? ProfileId, string Email, bool IsBlocked, IReadOnlyList<string> Roles);
