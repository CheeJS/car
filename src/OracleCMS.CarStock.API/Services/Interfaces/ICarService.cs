using OracleCMS.CarStock.API.Entities;

namespace OracleCMS.CarStock.API.Services.Interfaces;

public interface ICarService
{
    Task<IReadOnlyList<Car>> SearchAsync(
        int dealerId, string? make, string? model, CancellationToken cancellationToken = default);

    Task<Car?> GetByIdAsync(
        int dealerId, int id, CancellationToken cancellationToken = default);

    Task<AddCarResult> AddAsync(
        int dealerId, string make, string model, int year, int stock, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        int dealerId, int id, CancellationToken cancellationToken = default);

    Task<Car?> UpdateStockAsync(
        int dealerId, int id, int stock, CancellationToken cancellationToken = default);

    Task<AdjustStockResult> AdjustStockAsync(
        int dealerId, int id, int delta, CancellationToken cancellationToken = default);
}

public sealed record AddCarResult(Car Car);

public sealed record AdjustStockResult(AdjustStockStatus Status, Car? Car);

public enum AdjustStockStatus
{
    Updated,
    NotFound,
    WouldGoNegative
}
