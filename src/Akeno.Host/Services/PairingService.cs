using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace Akeno.Host.Services;

public sealed class PairingService
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _tokens = new();
    public string PairingCode { get; } = RandomNumberGenerator.GetInt32(100000, 999999).ToString();

    public string CreateToken(string code)
    {
        if (!CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(code.PadLeft(6, '0')),
                System.Text.Encoding.UTF8.GetBytes(PairingCode)))
            throw new UnauthorizedAccessException("Invalid pairing code.");

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        _tokens[token] = DateTimeOffset.UtcNow.AddDays(30);
        return token;
    }

    public bool IsValid(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return false;
        if (!_tokens.TryGetValue(token, out var expires)) return false;
        if (expires <= DateTimeOffset.UtcNow)
        {
            _tokens.TryRemove(token, out _);
            return false;
        }
        return true;
    }
}
