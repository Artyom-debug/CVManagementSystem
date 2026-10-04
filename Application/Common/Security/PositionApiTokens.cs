using System.Security.Cryptography;
using System.Text;

namespace Application.Common.Security;

internal static class PositionApiTokens
{
    private const string Prefix = "odoo_";
    private const int RandomBytes = 32;

    public static string Generate() => Prefix + Convert.ToHexString(RandomNumberGenerator.GetBytes(RandomBytes));

    public static bool IsValidFormat(string? token) =>
        token is not null && token.Length == Prefix.Length + RandomBytes * 2 &&
        token.StartsWith(Prefix, StringComparison.Ordinal) &&
        token.AsSpan(Prefix.Length).IndexOfAnyExcept("0123456789ABCDEF") < 0;

    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    public static string CacheKey(string tokenHash) => $"odoo:position-import:{tokenHash}";
}
