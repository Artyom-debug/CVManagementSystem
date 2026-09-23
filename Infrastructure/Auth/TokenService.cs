using Application.Common.Models;
using Application.Interfaces;
using Infrastructure.Entities;
using Infrastructure.Repositories;
using Infrastructure.Services.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Infrastructure.Auth;

internal sealed class TokenService : ITokenService
{
    internal const string SecurityStampClaim = "security_stamp";

    private readonly JwtOptions _jwtOptions;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;

    public TokenService(IOptions<JwtOptions> jwtOptions, IRefreshTokenRepository refreshTokenRepository, UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager)
    {
        _jwtOptions = jwtOptions.Value;
        _refreshTokenRepository = refreshTokenRepository;
        _userManager = userManager;
        _signInManager = signInManager;
    }

    public async Task<TokenPair> GenerateTokenPairAsync(string userId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User id cannot be empty.", nameof(userId));

        var user = await GetAllowedUserAsync(userId);
        var accessToken = await GenerateAccessTokenAsync(user);
        var refreshToken = GenerateRefreshToken(user.Id);

        await _refreshTokenRepository.StoreAsync(refreshToken.Entity, cancellationToken);

        return new TokenPair
        {
            AccessToken = accessToken.Token,
            RefreshToken = refreshToken.PlainText,
            AccessTokenExpiresAt = accessToken.ExpiresAtUtc,
            RefreshTokenExpiresAt = refreshToken.Entity.ExpiresAtUtc
        };
    }

    public async Task<TokenPair> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            throw new UnauthorizedAccessException("Invalid refresh token.");

        var tokenHash = Sha256Hex(refreshToken);
        var storedToken = await _refreshTokenRepository.GetByHashAsync(tokenHash, cancellationToken)
            ?? throw new UnauthorizedAccessException("Invalid refresh token.");

        if (storedToken.IsRevoked)
        {
            await _refreshTokenRepository.RevokeAllAsync(storedToken.UserId, cancellationToken);

            throw new UnauthorizedAccessException("Refresh token reuse detected.");
        }

        if (storedToken.IsExpired(DateTime.UtcNow))
        {
            await _refreshTokenRepository.RevokeAsync(storedToken, cancellationToken);

            throw new UnauthorizedAccessException("Refresh token expired.");
        }

        await GetAllowedUserAsync(storedToken.UserId);

        await _refreshTokenRepository.RevokeAsync(storedToken, cancellationToken);

        return await GenerateTokenPairAsync(storedToken.UserId, cancellationToken);
    }

    public async Task RevokeAllTokensAsync(string userId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User id cannot be empty.", nameof(userId));

        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new InvalidOperationException("User was not found.");

        await _refreshTokenRepository.RevokeAllAsync(userId, cancellationToken);

        var result = await _userManager.UpdateSecurityStampAsync(user);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(error => error.Description)));
        }
    }

    private async Task<ApplicationUser> GetAllowedUserAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId) ?? throw new UnauthorizedAccessException("User was not found.");

        if (!await _signInManager.CanSignInAsync(user) || await _userManager.IsLockedOutAsync(user))
            throw new UnauthorizedAccessException("User cannot sign in.");

        return user;
    }

    private async Task<(string Token, DateTime ExpiresAtUtc)> GenerateAccessTokenAsync(ApplicationUser user)
    {
        var key = Convert.FromBase64String(_jwtOptions.SecretKey);
        var now = DateTime.UtcNow;
        var expiresAtUtc = now.AddMinutes(_jwtOptions.TokenValidityMins);
        var signingCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(ClaimTypes.NameIdentifier, user.Id),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        if (!string.IsNullOrWhiteSpace(user.UserName))
            claims.Add(new Claim(ClaimTypes.Name, user.UserName));

        if (!string.IsNullOrWhiteSpace(user.Email))
            claims.Add(new Claim(ClaimTypes.Email, user.Email));

        var securityStamp = await _userManager.GetSecurityStampAsync(user);
        if (!string.IsNullOrWhiteSpace(securityStamp))
            claims.Add(new Claim(SecurityStampClaim, securityStamp));

        var roles = await _userManager.GetRolesAsync(user);
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _jwtOptions.Issuer,
            Audience = _jwtOptions.Audience,
            Subject = new ClaimsIdentity(claims),
            NotBefore = now,
            Expires = expiresAtUtc,
            SigningCredentials = signingCredentials
        };

        return (new JsonWebTokenHandler().CreateToken(descriptor), expiresAtUtc);
    }

    private (string PlainText, RefreshToken Entity) GenerateRefreshToken(string userId)
    {
        var now = DateTime.UtcNow;
        var expiresAtUtc = now.AddDays(_jwtOptions.RefreshTokenValidityDays);
        var plainText = Base64Url(RandomNumberGenerator.GetBytes(64));
        var hash = Sha256Hex(plainText);

        return (
            plainText,
            new RefreshToken(userId, hash, now, expiresAtUtc));
    }

    private static string Sha256Hex(string input) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input)));

    private static string Base64Url(byte[] data) =>
        Convert.ToBase64String(data)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
