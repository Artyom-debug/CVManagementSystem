namespace Application.Dtos;

public sealed record IdentityUserDto(string Id, string Email, bool IsBlocked, IReadOnlyList<string> Roles);
