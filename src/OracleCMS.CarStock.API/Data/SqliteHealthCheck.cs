using Dapper;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace OracleCMS.CarStock.API.Data;

public sealed class SqliteHealthCheck : IHealthCheck
{
    private readonly ISqliteConnectionFactory _factory;

    public SqliteHealthCheck(ISqliteConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = _factory.Create();
            var result = await connection.ExecuteScalarAsync<int>(
                new CommandDefinition("SELECT 1;", cancellationToken: cancellationToken));

            return result == 1
                ? HealthCheckResult.Healthy("SQLite reachable.")
                : HealthCheckResult.Unhealthy($"Unexpected probe result: {result}.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("SQLite probe failed.", ex);
        }
    }
}
