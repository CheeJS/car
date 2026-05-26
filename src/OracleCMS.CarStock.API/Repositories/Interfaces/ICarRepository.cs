using OracleCMS.CarStock.API.Entities;

namespace OracleCMS.CarStock.API.Repositories.Interfaces;

public interface ICarRepository
{
    Task<IReadOnlyList<Car>> SearchAsync(
        int dealerId, string? make, string? model, CancellationToken cancellationToken = default);

    Task<Car?> GetByIdAsync(
        int dealerId, int id, CancellationToken cancellationToken = default);

    Task<int> AddAsync(
        int dealerId, string make, string model, int year, int stock, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        int dealerId, int id, CancellationToken cancellationToken = default);

    Task<bool> UpdateStockAsync(
        int dealerId, int id, int stock, CancellationToken cancellationToken = default);
}
