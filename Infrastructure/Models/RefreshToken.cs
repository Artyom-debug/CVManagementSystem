using Domain.Abstractions;
using Infrastructure.Services.Identity;

namespace Infrastructure.Entities;

public sealed class RefreshToken : BaseEntity
{
    public string UserId { get; private set; } = null!;
    public ApplicationUser? User { get; private set; }
    public string TokenHash { get; private set; } = null!;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public bool IsRevoked { get; private set; }

    private RefreshToken()
    {
    }

    public RefreshToken(string userId, string tokenHash, DateTime createdAtUtc, DateTime expiresAtUtc)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User id cannot be empty.", nameof(userId));
        if (string.IsNullOrWhiteSpace(tokenHash))
            throw new ArgumentException("Token hash cannot be empty.", nameof(tokenHash));
        if (expiresAtUtc <= createdAtUtc)
            throw new ArgumentException("Refresh token expiration must be after its creation time.", nameof(expiresAtUtc));

        UserId = userId;
        TokenHash = tokenHash;
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = expiresAtUtc;
    }

    public bool IsExpired(DateTime utcNow) => utcNow >= ExpiresAtUtc;

    public void Revoke()
    {
        IsRevoked = true;
    }
}
