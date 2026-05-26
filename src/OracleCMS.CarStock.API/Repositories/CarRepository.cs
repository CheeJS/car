using Dapper;
using OracleCMS.CarStock.API.Data;
using OracleCMS.CarStock.API.Entities;
using OracleCMS.CarStock.API.Repositories.Interfaces;

namespace OracleCMS.CarStock.API.Repositories;

public sealed class CarRepository : ICarRepository
{
    private readonly ISqliteConnectionFactory _factory;

    public CarRepository(ISqliteConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<IReadOnlyList<Car>> SearchAsync(
        int dealerId, string? make, string? model, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT Id, DealerId, Make, Model, Year, Stock
            FROM Cars
            WHERE DealerId = @DealerId
              AND (@Make  IS NULL OR Make  LIKE '%' || @Make  || '%' COLLATE NOCASE)
              AND (@Model IS NULL OR Model LIKE '%' || @Model || '%' COLLATE NOCASE)
            ORDER BY Id;";

        using var connection = _factory.Create();
        var command = new CommandDefinition(
            sql,
            new
            {
                DealerId = dealerId,
                Make = NullIfBlank(make),
                Model = NullIfBlank(model)
            },
            cancellationToken: cancellationToken);

        var rows = await connection.QueryAsync<Car>(command);
        return rows.AsList();
    }

    public async Task<Car?> GetByIdAsync(
        int dealerId, int id, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT Id, DealerId, Make, Model, Year, Stock
            FROM Cars
            WHERE Id = @Id AND DealerId = @DealerId
            LIMIT 1;";

        using var connection = _factory.Create();
        var command = new CommandDefinition(
            sql,
            new { Id = id, DealerId = dealerId },
            cancellationToken: cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<Car>(command);
    }

    public async Task<int> AddAsync(
        int dealerId, string make, string model, int year, int stock, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            INSERT INTO Cars (DealerId, Make, Model, Year, Stock)
            VALUES (@DealerId, @Make, @Model, @Year, @Stock);
            SELECT last_insert_rowid();";

        using var connection = _factory.Create();
        var command = new CommandDefinition(
            sql,
            new
            {
                DealerId = dealerId,
                Make = make,
                Model = model,
                Year = year,
                Stock = stock
            },
            cancellationToken: cancellationToken);
        return await connection.ExecuteScalarAsync<int>(command);
    }

    public async Task<bool> DeleteAsync(
        int dealerId, int id, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            DELETE FROM Cars
            WHERE Id = @Id AND DealerId = @DealerId;";

        using var connection = _factory.Create();
        var command = new CommandDefinition(
            sql,
            new { Id = id, DealerId = dealerId },
            cancellationToken: cancellationToken);
        var rows = await connection.ExecuteAsync(command);
        return rows > 0;
    }

    public async Task<bool> UpdateStockAsync(
        int dealerId, int id, int stock, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            UPDATE Cars
            SET Stock = @Stock
            WHERE Id = @Id AND DealerId = @DealerId;";

        using var connection = _factory.Create();
        var command = new CommandDefinition(
            sql,
            new { Id = id, DealerId = dealerId, Stock = stock },
            cancellationToken: cancellationToken);
        var rows = await connection.ExecuteAsync(command);
        return rows > 0;
    }

    private static string? NullIfBlank(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
