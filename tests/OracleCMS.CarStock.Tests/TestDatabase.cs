using Microsoft.Data.Sqlite;
using OracleCMS.CarStock.API.Data;

namespace OracleCMS.CarStock.Tests;

public sealed class TestDatabase : IDisposable
{
    private readonly SqliteConnection _keepAlive;

    public TestDatabase()
    {
        var name = $"carstock-tests-{Guid.NewGuid():N}";
        ConnectionString = $"Data Source=file:{name}?mode=memory&cache=shared";

        _keepAlive = new SqliteConnection(ConnectionString);
        _keepAlive.Open();

        DatabaseInitializer.Initialize(ConnectionString);
    }

    public string ConnectionString { get; }

    public ISqliteConnectionFactory Factory => new SqliteConnectionFactory(ConnectionString);

    public void Dispose()
    {
        _keepAlive.Close();
        _keepAlive.Dispose();
    }
}
