namespace OracleCMS.CarStock.API.Services.Interfaces;

public interface IAuthService
{
    Task<RegisterOutcome> RegisterAsync(string email, string password, CancellationToken cancellationToken = default);
    Task<string?> LoginAsync(string email, string password, CancellationToken cancellationToken = default);
}

public enum RegisterOutcome
{
    Created,
    EmailAlreadyRegistered
}
