using Application.Common.Models;
using Application.Dtos;

namespace Application.Interfaces;

public interface IIdentityService
{
    Task<string?> GetUserNameAsync(string userId);

    Task<bool> IsInRoleAsync(string userId, string role);

    Task<(Result Result, string UserId)> CreateUserAsync(string userName, string password, string email, CancellationToken cancellationToken);

    Task<(Result Result, string UserId)> VerifyUserPasswordAsync(string password, string email, CancellationToken cancellationToken);

    Task<Result> ConfirmEmailAsync(string userId, string token);

    Task<PageResult<IdentityUserDto>> GetUsersAsync(int page, int pageSize, string? search, string? role, bool? isBlocked, CancellationToken cancellationToken);

    Task<Result> BlockUserAsync(string userId, CancellationToken cancellationToken);

    Task<Result> UnblockUserAsync(string userId, CancellationToken cancellationToken);

    Task<Result> DeleteUserAsync(string userId, CancellationToken cancellationToken);

    Task<Result> AddUserToRoleAsync(string userId, string role, CancellationToken cancellationToken);

    Task<Result> RemoveUserFromRoleAsync(string userId, string role, CancellationToken cancellationToken);
}
