using Microsoft.Data.Sqlite;

namespace Akeno.Host.Services;

public sealed class AkenoDbService
{
    private readonly string _connectionString;

    public AkenoDbService(IWebHostEnvironment environment)
    {
        var dataDir = Path.Combine(environment.ContentRootPath, "data");
        Directory.CreateDirectory(dataDir);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(dataDir, "akeno.db"),
            Mode = SqliteOpenMode.ReadWriteCreate,
            ForeignKeys = true
        }.ToString();
        Initialize();
    }

    private void Initialize()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = @"
CREATE TABLE IF NOT EXISTS kv_store (
  key TEXT PRIMARY KEY,
  value TEXT NOT NULL,
  updated_at TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS paired_clients (
  token TEXT PRIMARY KEY,
  device_name TEXT NOT NULL,
  created_at TEXT NOT NULL,
  expires_at TEXT NOT NULL,
  last_connected TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS app_actions (
  id TEXT PRIMARY KEY,
  name TEXT NOT NULL,
  path TEXT NOT NULL,
  arguments TEXT,
  working_directory TEXT,
  icon TEXT
);
";
        command.ExecuteNonQuery();
    }

    public async Task<string?> GetJsonAsync(string key, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT value FROM kv_store WHERE key = $key LIMIT 1";
        command.Parameters.AddWithValue("$key", key);
        return (string?)await command.ExecuteScalarAsync(cancellationToken);
    }

    public async Task SetJsonAsync(string key, string json, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = @"
INSERT INTO kv_store(key, value, updated_at)
VALUES($key, $value, $updated)
ON CONFLICT(key)
DO UPDATE SET value = excluded.value, updated_at = excluded.updated_at;";
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", json);
        command.Parameters.AddWithValue("$updated", DateTimeOffset.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpsertClientTokenAsync(string token, string deviceName, DateTimeOffset expiresAt, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        var now = DateTimeOffset.UtcNow.ToString("O");
        command.CommandText = @"
INSERT INTO paired_clients(token, device_name, created_at, expires_at, last_connected)
VALUES($token, $name, $created, $expires, $last)
ON CONFLICT(token)
DO UPDATE SET device_name = excluded.device_name, expires_at = excluded.expires_at, last_connected = excluded.last_connected;";
        command.Parameters.AddWithValue("$token", token);
        command.Parameters.AddWithValue("$name", deviceName);
        command.Parameters.AddWithValue("$created", now);
        command.Parameters.AddWithValue("$expires", expiresAt.ToString("O"));
        command.Parameters.AddWithValue("$last", now);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<bool> IsTokenValidAsync(string token, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT expires_at FROM paired_clients WHERE token = $token LIMIT 1";
        command.Parameters.AddWithValue("$token", token);
        var value = (string?)await command.ExecuteScalarAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(value) || !DateTimeOffset.TryParse(value, out var expiresAt))
        {
            return false;
        }

        return expiresAt > DateTimeOffset.UtcNow;
    }

    public async Task TouchClientAsync(string token, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE paired_clients SET last_connected = $last WHERE token = $token";
        command.Parameters.AddWithValue("$token", token);
        command.Parameters.AddWithValue("$last", DateTimeOffset.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PairedClient>> GetClientsAsync(CancellationToken cancellationToken = default)
    {
        var result = new List<PairedClient>();
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT token, device_name, last_connected, expires_at FROM paired_clients ORDER BY last_connected DESC";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new PairedClient(
                Token: reader.GetString(0),
                DeviceName: reader.GetString(1),
                LastConnected: DateTimeOffset.Parse(reader.GetString(2)),
                ExpiresAt: DateTimeOffset.Parse(reader.GetString(3))));
        }

        return result;
    }

    public async Task RevokeClientAsync(string token, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM paired_clients WHERE token = $token";
        command.Parameters.AddWithValue("$token", token);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}

public sealed record PairedClient(string Token, string DeviceName, DateTimeOffset LastConnected, DateTimeOffset ExpiresAt);
