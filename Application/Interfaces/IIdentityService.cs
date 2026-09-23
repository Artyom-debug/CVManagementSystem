using Application.Common.Models;
using Application.Dtos;

namespace Application.Interfaces;

public interface IIdentityService
{
    Task<IReadOnlyDictionary<string, string>> GetUserEmailsAsync(IReadOnlyCollection<string> userIds, CancellationToken cancellationToken);
    Task<string?> GetUserNameAsync(string userId);

    Task<bool> IsInRoleAsync(string userId, string role);

    Task<(Result Result, string ConfirmationToken)> StartRegistrationAsync(string password, string email, CancellationToken cancellationToken);

    Task<(Result Result, string ConfirmationToken)> ResendConfirmationEmailAsync(string email, CancellationToken cancellationToken);

    Task<(Result Result, string UserId)> VerifyUserPasswordAsync(string password, string email, CancellationToken cancellationToken);

    Task<(Result Result, string UserId)> SignInWithExternalProviderAsync(string provider, string providerKey, string email, CancellationToken cancellationToken);

    Task<Result> ConfirmEmailAsync(string token, CancellationToken cancellationToken);

    Task<PageResult<IdentityUserDto>> GetUsersAsync(int page, int pageSize, string? search, CancellationToken cancellationToken);

    Task<Result> BlockUserAsync(string userId, CancellationToken cancellationToken);

    Task<Result> UnblockUserAsync(string userId, CancellationToken cancellationToken);

    Task<Result> DeleteUserAsync(string userId, CancellationToken cancellationToken);

    Task<Result> AddUserToRoleAsync(string userId, string role, CancellationToken cancellationToken);

    Task<Result> RemoveUserFromRoleAsync(string userId, string role, CancellationToken cancellationToken);
}
