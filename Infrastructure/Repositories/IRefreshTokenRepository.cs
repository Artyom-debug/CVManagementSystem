using Infrastructure.Entities;

namespace Infrastructure.Repositories;

internal interface IRefreshTokenRepository
{
    Task StoreAsync(RefreshToken refreshToken, CancellationToken cancellationToken);

    Task<RefreshToken?> GetByHashAsync(string hash, CancellationToken cancellationToken);

    Task RevokeAsync(RefreshToken refreshToken, CancellationToken cancellationToken);

    Task RevokeAllAsync(string userId, CancellationToken cancellationToken);
}
