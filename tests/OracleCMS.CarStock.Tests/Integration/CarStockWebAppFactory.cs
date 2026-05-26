using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace OracleCMS.CarStock.Tests.Integration;

public sealed class CarStockWebAppFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;
    private readonly SqliteConnection _keepAlive;

    public CarStockWebAppFactory()
    {
        var name = $"carstock-integration-{Guid.NewGuid():N}";
        _connectionString = $"Data Source=file:{name}?mode=memory&cache=shared";

        _keepAlive = new SqliteConnection(_connectionString);
        _keepAlive.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _connectionString
            });
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _keepAlive.Close();
            _keepAlive.Dispose();
        }
        base.Dispose(disposing);
    }
}
