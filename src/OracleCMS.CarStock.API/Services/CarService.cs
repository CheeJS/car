using OracleCMS.CarStock.API.Entities;
using OracleCMS.CarStock.API.Repositories.Interfaces;
using OracleCMS.CarStock.API.Services.Interfaces;
using RepoOutcome = OracleCMS.CarStock.API.Repositories.Interfaces.AdjustStockOutcome;

namespace OracleCMS.CarStock.API.Services;

public sealed class CarService : ICarService
{
    private readonly ICarRepository _cars;

    public CarService(ICarRepository cars)
    {
        _cars = cars;
    }

    public Task<IReadOnlyList<Car>> SearchAsync(
        int dealerId, string? make, string? model, CancellationToken cancellationToken = default)
        => _cars.SearchAsync(dealerId, make, model, cancellationToken);

    public Task<Car?> GetByIdAsync(
        int dealerId, int id, CancellationToken cancellationToken = default)
        => _cars.GetByIdAsync(dealerId, id, cancellationToken);

    public async Task<AddCarResult> AddAsync(
        int dealerId, string make, string model, int year, int stock, CancellationToken cancellationToken = default)
    {
        var trimmedMake = make.Trim();
        var trimmedModel = model.Trim();

        var newId = await _cars.AddAsync(dealerId, trimmedMake, trimmedModel, year, stock, cancellationToken);

        return new AddCarResult(new Car
        {
            Id = newId,
            DealerId = dealerId,
            Make = trimmedMake,
            Model = trimmedModel,
            Year = year,
            Stock = stock
        });
    }

    public Task<bool> DeleteAsync(
        int dealerId, int id, CancellationToken cancellationToken = default)
        => _cars.DeleteAsync(dealerId, id, cancellationToken);

    public async Task<Car?> UpdateStockAsync(
        int dealerId, int id, int stock, CancellationToken cancellationToken = default)
    {
        var updated = await _cars.UpdateStockAsync(dealerId, id, stock, cancellationToken);
        if (!updated) return null;
        return await _cars.GetByIdAsync(dealerId, id, cancellationToken);
    }

    public async Task<AdjustStockResult> AdjustStockAsync(
        int dealerId, int id, int delta, CancellationToken cancellationToken = default)
    {
        var outcome = await _cars.AdjustStockAsync(dealerId, id, delta, cancellationToken);

        return outcome switch
        {
            RepoOutcome.Updated => new AdjustStockResult(
                AdjustStockStatus.Updated,
                await _cars.GetByIdAsync(dealerId, id, cancellationToken)),
            RepoOutcome.WouldGoNegative => new AdjustStockResult(AdjustStockStatus.WouldGoNegative, null),
            RepoOutcome.NotFound => new AdjustStockResult(AdjustStockStatus.NotFound, null),
            _ => throw new InvalidOperationException($"Unhandled adjust outcome: {outcome}.")
        };
    }
}
