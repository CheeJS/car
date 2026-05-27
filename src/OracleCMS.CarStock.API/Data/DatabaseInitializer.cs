using System.Data;
using Dapper;
using Microsoft.Data.Sqlite;

namespace OracleCMS.CarStock.API.Data;

public interface ISqliteConnectionFactory
{
    IDbConnection Create();
}

public sealed class SqliteConnectionFactory : ISqliteConnectionFactory
{
    private readonly string _connectionString;

    public SqliteConnectionFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    public IDbConnection Create() => new SqliteConnection(_connectionString);
}

public static class DatabaseInitializer
{
    public static void Initialize(string connectionString)
    {
        EnsureDataDirectoryExists(connectionString);

        using var connection = new SqliteConnection(connectionString);
        connection.Open();

        const string schema = @"
            CREATE TABLE IF NOT EXISTS Dealers (
                Id        INTEGER PRIMARY KEY AUTOINCREMENT,
                Email     TEXT    NOT NULL UNIQUE COLLATE NOCASE,
                Password  TEXT    NOT NULL
            );

            CREATE TABLE IF NOT EXISTS Cars (
                Id        INTEGER PRIMARY KEY AUTOINCREMENT,
                DealerId  INTEGER NOT NULL REFERENCES Dealers(Id),
                Make      TEXT    NOT NULL,
                Model     TEXT    NOT NULL,
                Year      INTEGER NOT NULL,
                Stock     INTEGER NOT NULL DEFAULT 0,
                CreatedAt TEXT    NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%SZ', 'now')),
                UpdatedAt TEXT    NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%SZ', 'now'))
            );

            CREATE INDEX IF NOT EXISTS IX_Cars_DealerId ON Cars(DealerId);
            CREATE INDEX IF NOT EXISTS IX_Cars_DealerId_Make_Model ON Cars(DealerId, Make, Model);
        ";

        connection.Execute(schema);
    }

    private static void EnsureDataDirectoryExists(string connectionString)
    {
        var builder = new SqliteConnectionStringBuilder(connectionString);
        var dataSource = builder.DataSource;
        if (string.IsNullOrWhiteSpace(dataSource)) return;

        var directory = Path.GetDirectoryName(Path.GetFullPath(dataSource));
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}
