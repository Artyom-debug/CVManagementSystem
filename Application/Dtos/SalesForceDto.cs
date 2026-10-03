namespace Application.Dtos;

public sealed record SalesForceDto(
    string FirstName,
    string LastName,
    string Email,
    string? OrganizationName = null,
    string? OrganizationPhone = null,
    string? OrganizationWebSite = null,
    string? Industry = null,
    string? Position = null,
    string? Phone = null);
