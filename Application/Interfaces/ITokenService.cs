using Application.Common.Models;

namespace Application.Interfaces;

public interface ITokenService
{
    Task<TokenPair> GenerateTokenPairAsync(string userId, CancellationToken cancellationToken);

    Task<TokenPair> RefreshAsync(string refreshToken, CancellationToken cancellationToken);

    Task RevokeAllTokensAsync(string userId, CancellationToken cancellationToken);
}
