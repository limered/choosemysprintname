using Microsoft.Data.Sqlite;

namespace Backend.Sessions;

public interface IWinnerHistoryStore
{
    Task SaveWinnerAsync(string name, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetAllWinnerNamesAsync(CancellationToken ct = default);
}

/// <summary>
/// SQLite-backed implementation of <see cref="IWinnerHistoryStore"/>. The
/// schema is created automatically on construction. Names are stored exactly
/// as supplied (case-sensitive); <see cref="GetAllWinnerNamesAsync"/> returns
/// distinct names. A connection is opened per operation to keep things
/// thread-safe; SQLite is plenty fast for this workload.
/// </summary>
public sealed class SqliteWinnerHistoryStore : IWinnerHistoryStore
{
    private readonly string _connectionString;

    public SqliteWinnerHistoryStore(string dbPath)
    {
        if (string.IsNullOrWhiteSpace(dbPath))
            throw new ArgumentException("dbPath must not be empty", nameof(dbPath));

        // Ensure parent directory exists if a relative/absolute path is given.
        var fullPath = Path.GetFullPath(dbPath);
        var dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = fullPath,
        }.ToString();

        EnsureSchema();
    }

    private void EnsureSchema()
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS winners (
                name TEXT NOT NULL,
                created_at_utc TEXT NOT NULL
            );";
        cmd.ExecuteNonQuery();
    }

    public async Task SaveWinnerAsync(string name, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("name must not be empty", nameof(name));

        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO winners (name, created_at_utc) VALUES ($name, $ts);";
        cmd.Parameters.AddWithValue("$name", name);
        cmd.Parameters.AddWithValue("$ts", DateTimeOffset.UtcNow.ToString("O"));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<string>> GetAllWinnerNamesAsync(CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT DISTINCT name FROM winners;";
        var results = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            results.Add(reader.GetString(0));
        }
        return results;
    }
}
