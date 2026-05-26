using OracleCMS.CarStock.API.Entities;

namespace OracleCMS.CarStock.API.Repositories.Interfaces;

public interface IDealerRepository
{
    Task<Dealer?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default);
    Task<int> CreateAsync(string email, string passwordHash, CancellationToken cancellationToken = default);
}
