namespace OracleCMS.CarStock.API.Entities;

public sealed class Dealer
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;

    /// <summary>BCrypt-hashed password. Stored in the <c>Dealers.Password</c> column for backward-compatibility with the schema.</summary>
    public string PasswordHash { get; set; } = string.Empty;
}
