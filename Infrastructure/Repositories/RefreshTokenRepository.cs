using Infrastructure.Data;
using Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

internal sealed class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly ApplicationDbContext _context;

    public RefreshTokenRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task StoreAsync(RefreshToken refreshToken, CancellationToken cancellationToken)
    {
        await _context.RefreshTokens.AddAsync(refreshToken, cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);
    }

    public Task<RefreshToken?> GetByHashAsync(string hash, CancellationToken cancellationToken)
    {
        return _context.RefreshTokens
            .SingleOrDefaultAsync(token => token.TokenHash == hash, cancellationToken);
    }

    public async Task RevokeAsync(RefreshToken refreshToken, CancellationToken cancellationToken)
    {
        refreshToken.Revoke();
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task RevokeAllAsync(string userId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var tokens = await _context.RefreshTokens
            .Where(token => token.UserId == userId && !token.IsRevoked && token.ExpiresAtUtc > now)
            .ToListAsync(cancellationToken);

        foreach (var token in tokens)
            token.Revoke();

        if (tokens.Count > 0)
            await _context.SaveChangesAsync(cancellationToken);
    }
}
