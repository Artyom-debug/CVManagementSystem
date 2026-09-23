using Application.Common.Models;
using Application.Constants;
using Application.Dtos;
using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using IdentityOperationResult = Microsoft.AspNetCore.Identity.IdentityResult;

namespace Infrastructure.Services.Identity;

public sealed class IdentityService : IIdentityService
{
    private static readonly TimeSpan PendingRegistrationLifetime = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan ResendConfirmationCooldown = TimeSpan.FromMinutes(1);
    private static readonly HashSet<string> SupportedRoles = new([Roles.Candidate, Roles.Recruiter, Roles.Administrator], StringComparer.OrdinalIgnoreCase);

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly ITokenService _tokenService;
    private readonly ApplicationDbContext _context;
    private readonly IDatabase _registrationDatabase;

    public IdentityService(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, RoleManager<IdentityRole> roleManager, ITokenService tokenService, ApplicationDbContext context, IConnectionMultiplexer redis)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _roleManager = roleManager;
        _tokenService = tokenService;
        _context = context;
        _registrationDatabase = redis.GetDatabase();
    }

    public async Task<string?> GetUserNameAsync(string userId)
    {
        var user = await FindUserAsync(userId);
        return user?.UserName;
    }

    public async Task<IReadOnlyDictionary<string, string>> GetUserEmailsAsync(IReadOnlyCollection<string> userIds, CancellationToken cancellationToken) =>
        await _userManager.Users.AsNoTracking()
            .Where(user => userIds.Contains(user.Id) && user.Email != null)
            .ToDictionaryAsync(user => user.Id, user => user.Email!, cancellationToken);

    public async Task<bool> IsInRoleAsync(string userId, string role)
    {
        var user = await FindUserAsync(userId);
        return user is not null &&
               !string.IsNullOrWhiteSpace(role) &&
               await _userManager.IsInRoleAsync(user, role);
    }

    public async Task<(Result Result, string ConfirmationToken)> StartRegistrationAsync(string password, string email, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email))
            return (Result.Failure("Email cannot be empty."), string.Empty);
        if (string.IsNullOrWhiteSpace(password))
            return (Result.Failure("Password cannot be empty."), string.Empty);

        cancellationToken.ThrowIfCancellationRequested();

        var normalizedEmail = email.Trim();
        if (await _userManager.FindByEmailAsync(normalizedEmail) is not null)
            return (Result.Failure("A user with this email already exists."), string.Empty);

        var pendingUser = new ApplicationUser
        {
            Email = normalizedEmail,
            UserName = normalizedEmail
        };

        var validationErrors = new List<string>();
        foreach (var validator in _userManager.UserValidators)
        {
            var validationResult = await validator.ValidateAsync(_userManager, pendingUser);
            validationErrors.AddRange(validationResult.Errors.Select(error => error.Description));
        }

        foreach (var validator in _userManager.PasswordValidators)
        {
            var validationResult = await validator.ValidateAsync(_userManager, pendingUser, password);
            validationErrors.AddRange(validationResult.Errors.Select(error => error.Description));
        }

        if (validationErrors.Count > 0)
            return (Result.Failure(validationErrors.Distinct()), string.Empty);

        var passwordHash = _userManager.PasswordHasher.HashPassword(pendingUser, password);
        var pendingRegistration = new PendingRegistration(normalizedEmail, passwordHash);
        var confirmationToken = CreateConfirmationToken();
        var saved = await SavePendingRegistrationAsync(confirmationToken, pendingRegistration, cancellationToken);

        return saved
            ? (Result.Success(), confirmationToken)
            : (Result.Failure("Could not start email confirmation."), string.Empty);
    }

    public async Task<(Result Result, string ConfirmationToken)> ResendConfirmationEmailAsync(string email, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email))
            return (Result.Failure("Email cannot be empty."), string.Empty);

        cancellationToken.ThrowIfCancellationRequested();

        var normalizedEmail = email.Trim();
        if (await _userManager.FindByEmailAsync(normalizedEmail) is not null)
            return (Result.Failure("A user with this email already exists."), string.Empty);

        var emailKey = GetPendingRegistrationEmailKey(normalizedEmail);
        var currentTokenHash = await _registrationDatabase.StringGetAsync(emailKey).WaitAsync(cancellationToken);
        if (currentTokenHash.IsNullOrEmpty)
            return (Result.Success(), string.Empty);

        var registrationJson = await _registrationDatabase
            .StringGetAsync(GetPendingRegistrationKeyByHash(currentTokenHash.ToString()))
            .WaitAsync(cancellationToken);

        if (registrationJson.IsNullOrEmpty)
        {
            await _registrationDatabase.KeyDeleteAsync(emailKey).WaitAsync(cancellationToken);
            return (Result.Success(), string.Empty);
        }

        var registration = JsonSerializer.Deserialize<PendingRegistration>(registrationJson.ToString());
        if (registration is null || !string.Equals(registration.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase))
            return (Result.Success(), string.Empty);

        var cooldownKey = GetResendConfirmationCooldownKey(normalizedEmail);
        var canResend = await _registrationDatabase
            .StringSetAsync(cooldownKey, "1", ResendConfirmationCooldown, When.NotExists)
            .WaitAsync(cancellationToken);

        if (!canResend)
            return (Result.Success(), string.Empty);

        var confirmationToken = CreateConfirmationToken();
        var saved = await SavePendingRegistrationAsync(confirmationToken, registration, cancellationToken);
        if (!saved)
            return (Result.Failure("Could not renew email confirmation."), string.Empty);

        return (Result.Success(), confirmationToken);
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

        if (signInResult.IsLockedOut)
            return (Result.Failure("Your account is blocked. Contact an administrator."), string.Empty);

        return signInResult.Succeeded
            ? (Result.Success(), user.Id)
            : InvalidCredentials();
    }

    public async Task<(Result Result, string UserId)> SignInWithExternalProviderAsync(string provider, string providerKey, string email, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(provider) || string.IsNullOrWhiteSpace(providerKey))
            return (Result.Failure("External login information is invalid."), string.Empty);
        if (string.IsNullOrWhiteSpace(email))
            return (Result.Failure("The external provider did not return an email address."), string.Empty);

        cancellationToken.ThrowIfCancellationRequested();

        var user = await _userManager.FindByLoginAsync(provider, providerKey);
        if (user is not null)
            return (Result.Success(), user.Id);

        var normalizedEmail = email.Trim();
        var userWithSameEmail = await _userManager.FindByEmailAsync(normalizedEmail);
        if (userWithSameEmail is not null)
        {
            return (
                Result.Failure("An account with this email already exists. Sign in with that account before linking an external provider."),
                string.Empty);
        }

        if (!await _roleManager.RoleExistsAsync(Roles.Candidate))
        {
            return (
                Result.Failure($"Required role '{Roles.Candidate}' has not been created."),
                string.Empty);
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        user = new ApplicationUser
        {
            UserName = normalizedEmail,
            Email = normalizedEmail,
            EmailConfirmed = true
        };

        var createResult = await _userManager.CreateAsync(user);
        if (!createResult.Succeeded)
            return (ToResult(createResult), string.Empty);

        var addLoginResult = await _userManager.AddLoginAsync(user, new UserLoginInfo(provider, providerKey, provider));
        if (!addLoginResult.Succeeded)
            return (ToResult(addLoginResult), string.Empty);

        var addRoleResult = await _userManager.AddToRoleAsync(user, Roles.Candidate);
        if (!addRoleResult.Succeeded)
            return (ToResult(addRoleResult), string.Empty);

        _context.Profiles.Add(new Profile(user.Id));
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return (Result.Success(), user.Id);
    }

    public async Task<Result> ConfirmEmailAsync(string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
            return Result.Failure("Email confirmation token cannot be empty.");

        var registrationKey = GetPendingRegistrationKey(token);
        var registrationJson = await _registrationDatabase
            .StringGetAsync(registrationKey)
            .WaitAsync(cancellationToken);

        if (registrationJson.IsNullOrEmpty)
            return Result.Failure("Email confirmation token is invalid or expired.");

        var registration = JsonSerializer.Deserialize<PendingRegistration>(registrationJson.ToString());
        if (registration is null)
            return Result.Failure("Pending registration data is invalid.");

        var tokenHash = GetTokenHash(token);
        var emailKey = GetPendingRegistrationEmailKey(registration.Email);
        var currentTokenHash = await _registrationDatabase.StringGetAsync(emailKey).WaitAsync(cancellationToken);
        if (!currentTokenHash.IsNullOrEmpty && !string.Equals(currentTokenHash.ToString(), tokenHash, StringComparison.Ordinal))
            return Result.Failure("Email confirmation token is invalid or expired.");

        if (await _userManager.FindByEmailAsync(registration.Email) is not null)
            return Result.Failure("A user with this email already exists.");

        if (!await _roleManager.RoleExistsAsync(Roles.Candidate))
            return Result.Failure($"Required role '{Roles.Candidate}' has not been created.");

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        var user = new ApplicationUser
        {
            Email = registration.Email,
            UserName = registration.Email,
            EmailConfirmed = true,
            PasswordHash = registration.PasswordHash
        };

        var createResult = await _userManager.CreateAsync(user);
        if (!createResult.Succeeded)
            return ToResult(createResult);

        var roleResult = await _userManager.AddToRoleAsync(user, Roles.Candidate);
        if (!roleResult.Succeeded)
            return ToResult(roleResult);

        _context.Profiles.Add(new Profile(user.Id));
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await _registrationDatabase.KeyDeleteAsync([registrationKey, emailKey]).WaitAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<PageResult<IdentityUserDto>> GetUsersAsync(int page, int pageSize, string? search, CancellationToken cancellationToken)
    {
        ValidatePagination(page, pageSize);

        var query = _context.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToUpperInvariant();
            query = query.Where(user => (user.NormalizedEmail != null && user.NormalizedEmail.Contains(normalizedSearch)) || (user.NormalizedUserName != null && user.NormalizedUserName.Contains(normalizedSearch)));
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
                user.LockoutEnd,
                ProfileId = _context.Profiles
                    .Where(profile => profile.UserId == user.Id)
                    .Select(profile => (Guid?)profile.Id)
                    .SingleOrDefault()
            })
            .ToListAsync(cancellationToken);

        var hasNextPage = users.Count > pageSize;
        if (hasNextPage)
            users.RemoveAt(users.Count - 1);

        var userIds = users.Select(user => user.Id).ToArray();
        var userRoles = await _context.UserRoles
            .AsNoTracking()
            .Where(userRole => userIds.Contains(userRole.UserId))
            .Join(
                _context.Roles.AsNoTracking(),
                userRole => userRole.RoleId,
                role => role.Id,
                (userRole, role) => new
                {
                    userRole.UserId,
                    Role = role.Name!
                })
            .ToListAsync(cancellationToken);

        var rolesByUser = userRoles
            .GroupBy(item => item.UserId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<string>)group.Select(item => item.Role).OrderBy(name => name).ToArray());
        var currentTime = DateTimeOffset.UtcNow;
        var items = users
            .Select(user => new IdentityUserDto(user.Id, user.ProfileId, user.Email, user.LockoutEnd.HasValue && user.LockoutEnd > currentTime, rolesByUser.GetValueOrDefault(user.Id, [])))
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

        // Adding permissions does not invalidate existing, less-privileged sessions.
        // Removing permissions must immediately revoke access and refresh tokens.
        if (!add)
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

    private async Task<bool> SavePendingRegistrationAsync(string confirmationToken, PendingRegistration registration, CancellationToken cancellationToken)
    {
        var tokenHash = GetTokenHash(confirmationToken);
        var registrationKey = GetPendingRegistrationKeyByHash(tokenHash);
        var emailKey = GetPendingRegistrationEmailKey(registration.Email);
        var previousTokenHash = await _registrationDatabase.StringGetAsync(emailKey).WaitAsync(cancellationToken);
        var transaction = _registrationDatabase.CreateTransaction();

        _ = transaction.StringSetAsync(registrationKey, JsonSerializer.Serialize(registration), PendingRegistrationLifetime);
        _ = transaction.StringSetAsync(emailKey, tokenHash, PendingRegistrationLifetime);

        if (!previousTokenHash.IsNullOrEmpty && !string.Equals(previousTokenHash.ToString(), tokenHash, StringComparison.Ordinal))
            _ = transaction.KeyDeleteAsync(GetPendingRegistrationKeyByHash(previousTokenHash.ToString()));

        return await transaction.ExecuteAsync().WaitAsync(cancellationToken);
    }

    private static string CreateConfirmationToken() =>
        WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(48));

    private static string GetPendingRegistrationKey(string confirmationToken) =>
        GetPendingRegistrationKeyByHash(GetTokenHash(confirmationToken));

    private static string GetPendingRegistrationKeyByHash(string tokenHash) =>
        $"pending-registration:{tokenHash}";

    private static string GetPendingRegistrationEmailKey(string email)
    {
        var emailHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(email.Trim().ToUpperInvariant())));
        return $"pending-registration-email:{emailHash}";
    }

    private static string GetResendConfirmationCooldownKey(string email) =>
        $"pending-registration-resend:{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(email.Trim().ToUpperInvariant())))}";

    private static string GetTokenHash(string confirmationToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(confirmationToken)));

    private static Result ToResult(IdentityOperationResult result) =>
        result.Succeeded
            ? Result.Success()
            : Result.Failure(result.Errors.Select(error => error.Description));

    private sealed record PendingRegistration(string Email, string PasswordHash);
}
