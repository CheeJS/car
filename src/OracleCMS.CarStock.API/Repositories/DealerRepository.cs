using Dapper;
using OracleCMS.CarStock.API.Data;
using OracleCMS.CarStock.API.Entities;
using OracleCMS.CarStock.API.Repositories.Interfaces;

namespace OracleCMS.CarStock.API.Repositories;

public sealed class DealerRepository : IDealerRepository
{
    private readonly ISqliteConnectionFactory _factory;

    public DealerRepository(ISqliteConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<Dealer?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT Id, Email, Password AS PasswordHash
            FROM Dealers
            WHERE Email = @Email COLLATE NOCASE
            LIMIT 1;";

        using var connection = _factory.Create();
        var command = new CommandDefinition(sql, new { Email = email }, cancellationToken: cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<Dealer>(command);
    }

    public async Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT 1
            FROM Dealers
            WHERE Email = @Email COLLATE NOCASE
            LIMIT 1;";

        using var connection = _factory.Create();
        var command = new CommandDefinition(sql, new { Email = email }, cancellationToken: cancellationToken);
        var result = await connection.ExecuteScalarAsync<int?>(command);
        return result.HasValue;
    }

    public async Task<int> CreateAsync(string email, string passwordHash, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            INSERT INTO Dealers (Email, Password)
            VALUES (@Email, @Password);
            SELECT last_insert_rowid();";

        using var connection = _factory.Create();
        var command = new CommandDefinition(
            sql,
            new { Email = email, Password = passwordHash },
            cancellationToken: cancellationToken);
        return await connection.ExecuteScalarAsync<int>(command);
    }
}
