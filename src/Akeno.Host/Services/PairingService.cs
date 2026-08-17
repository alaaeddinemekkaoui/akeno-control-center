using System.Security.Cryptography;
using System.Text;

namespace Akeno.Host.Services;

public sealed class PairingService
{
    private readonly AkenoDbService _db;

    public PairingService(AkenoDbService db)
    {
        _db = db;
    }

    public string PairingCode { get; } = RandomNumberGenerator.GetInt32(100000, 999999).ToString();

    public async Task<string> CreateTokenAsync(string code, string? deviceName, CancellationToken cancellationToken = default)
    {
        var normalizedCode = (code ?? string.Empty).Trim().PadLeft(6, '0');
        if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(normalizedCode), Encoding.UTF8.GetBytes(PairingCode)))
        {
            throw new UnauthorizedAccessException("Invalid pairing code.");
        }

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        await _db.UpsertClientTokenAsync(token, string.IsNullOrWhiteSpace(deviceName) ? "Unknown device" : deviceName.Trim(), DateTimeOffset.UtcNow.AddDays(30), cancellationToken);
        return token;
    }

    public async Task<bool> IsValidAsync(string? token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token)) return false;
        var isValid = await _db.IsTokenValidAsync(token, cancellationToken);
        if (isValid)
        {
            await _db.TouchClientAsync(token, cancellationToken);
        }

        return isValid;
    }

    public Task<IReadOnlyList<PairedClient>> GetClientsAsync(CancellationToken cancellationToken = default) => _db.GetClientsAsync(cancellationToken);

    public Task RevokeAsync(string token, CancellationToken cancellationToken = default) => _db.RevokeClientAsync(token, cancellationToken);
}
