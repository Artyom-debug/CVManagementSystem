using Application.Common.Models;
using Application.Constants;
using Application.Dtos;
using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using IdentityOperationResult = Microsoft.AspNetCore.Identity.IdentityResult;

namespace Infrastructure.Services.Identity;

public sealed class IdentityService : IIdentityService
{
    private static readonly HashSet<string> SupportedRoles = new([Roles.Candidate, Roles.Recruiter, Roles.Administrator], StringComparer.OrdinalIgnoreCase);

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly ITokenService _tokenService;
    private readonly ApplicationDbContext _context;

    public IdentityService(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, RoleManager<IdentityRole> roleManager, ITokenService tokenService, ApplicationDbContext context)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _roleManager = roleManager;
        _tokenService = tokenService;
        _context = context;
    }

    public async Task<string?> GetUserNameAsync(string userId)
    {
        var user = await FindUserAsync(userId);
        return user?.UserName;
    }

    public async Task<bool> IsInRoleAsync(string userId, string role)
    {
        var user = await FindUserAsync(userId);
        return user is not null &&
               !string.IsNullOrWhiteSpace(role) &&
               await _userManager.IsInRoleAsync(user, role);
    }

    public async Task<(Result Result, string UserId)> CreateUserAsync(string userName, string password, string email, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userName))
            return (Result.Failure("User name cannot be empty."), string.Empty);
        if (string.IsNullOrWhiteSpace(email))
            return (Result.Failure("Email cannot be empty."), string.Empty);
        if (string.IsNullOrWhiteSpace(password))
            return (Result.Failure("Password cannot be empty."), string.Empty);

        cancellationToken.ThrowIfCancellationRequested();

        if (!await _roleManager.RoleExistsAsync(Roles.Candidate))
        {
            return (
                Result.Failure($"Required role '{Roles.Candidate}' has not been created."),
                string.Empty);
        }

        await using var transaction = await _context.Database
            .BeginTransactionAsync(cancellationToken);

        var user = new ApplicationUser
        {
            UserName = userName.Trim(),
            Email = email.Trim()
        };

        var createResult = await _userManager.CreateAsync(user, password);
        if (!createResult.Succeeded)
            return (ToResult(createResult), string.Empty);

        var roleResult = await _userManager.AddToRoleAsync(user, Roles.Candidate);

        if (!roleResult.Succeeded)
            return (ToResult(roleResult), string.Empty);

        _context.Profiles.Add(new Profile(user.Id));
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return (Result.Success(), user.Id);
    }

    public async Task<(Result Result, string UserId)> VerifyUserPasswordAsync(string password, string email, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(password))
        {
            return InvalidCredentials();
        }

        cancellationToken.ThrowIfCancellationRequested();

        var user = await _userManager.FindByEmailAsync(email.Trim());
        if (user is null)
            return InvalidCredentials();

        var signInResult = await _signInManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);

        return signInResult.Succeeded
            ? (Result.Success(), user.Id)
            : InvalidCredentials();
    }

    public async Task<Result> ConfirmEmailAsync(string userId, string token)
    {
        var user = await FindUserAsync(userId);
        if (user is null)
            return Result.Failure("User was not found.");
        if (string.IsNullOrWhiteSpace(token))
            return Result.Failure("Email confirmation token cannot be empty.");

        return ToResult(await _userManager.ConfirmEmailAsync(user, token));
    }

    public async Task<PageResult<IdentityUserDto>> GetUsersAsync(int page, int pageSize, string? search, string? role, bool? isBlocked, CancellationToken cancellationToken)
    {
        ValidatePagination(page, pageSize);

        var query = _context.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToUpperInvariant();
            query = query.Where(user => (user.NormalizedEmail != null && user.NormalizedEmail.Contains(normalizedSearch)) || (user.NormalizedUserName != null && user.NormalizedUserName.Contains(normalizedSearch)));
        }

        if (!string.IsNullOrWhiteSpace(role))
        {
            var normalizedRole = role.Trim().ToUpperInvariant();
            query = query.Where(user => _context.UserRoles.Any(userRole => userRole.UserId == user.Id && _context.Roles.Any(existingRole => existingRole.Id == userRole.RoleId && existingRole.NormalizedName == normalizedRole)));
        }

        if (isBlocked.HasValue)
        {
            var filterTime = DateTimeOffset.UtcNow;
            query = isBlocked.Value
                ? query.Where(user => user.LockoutEnd.HasValue && user.LockoutEnd > filterTime)
                : query.Where(user => !user.LockoutEnd.HasValue || user.LockoutEnd <= filterTime);
        }

        var users = await query
            .OrderBy(user => user.Email)
            .ThenBy(user => user.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize + 1)
            .Select(user => new
            {
                user.Id,
                Email = user.Email ?? string.Empty,
                user.LockoutEnd
            })
            .ToListAsync(cancellationToken);

        var hasNextPage = users.Count > pageSize;
        if (hasNextPage)
            users.RemoveAt(users.Count - 1);

        var userIds = users.Select(user => user.Id).ToArray();
        var userRoles = await (
                from userRole in _context.UserRoles.AsNoTracking()
                join existingRole in _context.Roles.AsNoTracking()
                    on userRole.RoleId equals existingRole.Id
                where userIds.Contains(userRole.UserId)
                select new
                {
                    userRole.UserId,
                    Role = existingRole.Name!
                })
            .ToListAsync(cancellationToken);

        var rolesByUser = userRoles
            .GroupBy(item => item.UserId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<string>)group.Select(item => item.Role).OrderBy(name => name).ToArray());
        var currentTime = DateTimeOffset.UtcNow;
        var items = users
            .Select(user => new IdentityUserDto(user.Id, user.Email, user.LockoutEnd.HasValue && user.LockoutEnd > currentTime, rolesByUser.GetValueOrDefault(user.Id, [])))
            .ToList();

        return new PageResult<IdentityUserDto>(items, page, pageSize, hasNextPage);
    }

    public async Task<Result> BlockUserAsync(string userId, CancellationToken cancellationToken)
    {
        var user = await FindUserAsync(userId);
        if (user is null)
            return Result.Failure("User was not found.");

        cancellationToken.ThrowIfCancellationRequested();

        var enableResult = await _userManager.SetLockoutEnabledAsync(user, true);
        if (!enableResult.Succeeded)
            return ToResult(enableResult);

        var lockResult = await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
        if (!lockResult.Succeeded)
            return ToResult(lockResult);

        await _tokenService.RevokeAllTokensAsync(user.Id, cancellationToken);
        return Result.Success();
    }

    public async Task<Result> UnblockUserAsync(string userId, CancellationToken cancellationToken)
    {
        var user = await FindUserAsync(userId);
        if (user is null)
            return Result.Failure("User was not found.");

        cancellationToken.ThrowIfCancellationRequested();

        var unlockResult = await _userManager.SetLockoutEndDateAsync(user, null);
        if (!unlockResult.Succeeded)
            return ToResult(unlockResult);

        return ToResult(await _userManager.ResetAccessFailedCountAsync(user));
    }

    public async Task<Result> DeleteUserAsync(string userId, CancellationToken cancellationToken)
    {
        var user = await FindUserAsync(userId);
        if (user is null)
            return Result.Failure("User was not found.");

        await using var transaction = await _context.Database
            .BeginTransactionAsync(cancellationToken);

        var profile = await _context.Profiles
            .SingleOrDefaultAsync(profile => profile.UserId == user.Id, cancellationToken);

        if (profile is not null)
        {
            _context.Profiles.Remove(profile);
            await _context.SaveChangesAsync(cancellationToken);
        }

        var deleteResult = await _userManager.DeleteAsync(user);
        if (!deleteResult.Succeeded)
            return ToResult(deleteResult);

        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }

    public Task<Result> AddUserToRoleAsync(string userId, string role, CancellationToken cancellationToken) =>
        ChangeRoleAsync(userId, role, add: true, cancellationToken);

    public Task<Result> RemoveUserFromRoleAsync(string userId, string role, CancellationToken cancellationToken) =>
        ChangeRoleAsync(userId, role, add: false, cancellationToken);

    private async Task<Result> ChangeRoleAsync(string userId, string role, bool add, CancellationToken cancellationToken)
    {
        var user = await FindUserAsync(userId);
        if (user is null)
            return Result.Failure("User was not found.");
        if (string.IsNullOrWhiteSpace(role) || !SupportedRoles.Contains(role))
            return Result.Failure("Unknown user role.");
        if (!await _roleManager.RoleExistsAsync(role))
            return Result.Failure($"Role '{role}' has not been created.");

        cancellationToken.ThrowIfCancellationRequested();

        var alreadyInRole = await _userManager.IsInRoleAsync(user, role);
        if (add == alreadyInRole)
            return Result.Success();

        var identityResult = add
            ? await _userManager.AddToRoleAsync(user, role)
            : await _userManager.RemoveFromRoleAsync(user, role);

        if (!identityResult.Succeeded)
            return ToResult(identityResult);

        await _tokenService.RevokeAllTokensAsync(user.Id, cancellationToken);
        return Result.Success();
    }

    private Task<ApplicationUser?> FindUserAsync(string userId)
    {
        return string.IsNullOrWhiteSpace(userId)
            ? Task.FromResult<ApplicationUser?>(null)
            : _userManager.FindByIdAsync(userId);
    }

    private static void ValidatePagination(int page, int pageSize)
    {
        if (page < 1)
            throw new ArgumentOutOfRangeException(nameof(page));
        if (pageSize is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(pageSize));
        if ((long)(page - 1) * pageSize > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(page));
    }

    private static (Result Result, string UserId) InvalidCredentials() =>
        (Result.Failure("Invalid email or password."), string.Empty);

    private static Result ToResult(IdentityOperationResult result) =>
        result.Succeeded
            ? Result.Success()
            : Result.Failure(result.Errors.Select(error => error.Description));
}
