using Microsoft.Data.Sqlite;

namespace Majorsilence.Games.Server;

/// <summary>
/// Owns the SQLite connection string and schema. No EF Core / migrations
/// framework - four small tables, applied idempotently with CREATE TABLE IF
/// NOT EXISTS on startup, is simpler than a migrations pipeline at this scale.
/// </summary>
public class Db
{
    public string ConnectionString { get; }

    public Db(string dataSourcePath)
    {
        ConnectionString = new SqliteConnectionStringBuilder { DataSource = dataSourcePath }.ToString();
    }

    public SqliteConnection Open()
    {
        var connection = new SqliteConnection(ConnectionString);
        connection.Open();
        return connection;
    }

    public void EnsureSchema()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS accounts (
                id TEXT PRIMARY KEY,
                created_utc TEXT NOT NULL,
                email TEXT UNIQUE,
                password_hash TEXT
            );

            CREATE TABLE IF NOT EXISTS devices (
                id TEXT PRIMARY KEY,
                account_id TEXT NOT NULL REFERENCES accounts(id),
                token_hash TEXT NOT NULL UNIQUE,
                platform TEXT NOT NULL,
                created_utc TEXT NOT NULL,
                last_seen_utc TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_devices_account ON devices(account_id);

            CREATE TABLE IF NOT EXISTS saves (
                account_id TEXT PRIMARY KEY REFERENCES accounts(id),
                payload_json TEXT NOT NULL,
                updated_utc TEXT NOT NULL,
                revision INTEGER NOT NULL,
                device_id TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS link_codes (
                code TEXT PRIMARY KEY,
                account_id TEXT NOT NULL REFERENCES accounts(id),
                expires_utc TEXT NOT NULL
            );
            """;
        command.ExecuteNonQuery();
    }
}
