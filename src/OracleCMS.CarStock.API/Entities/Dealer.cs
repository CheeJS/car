namespace OracleCMS.CarStock.API.Entities;

public sealed class Dealer
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
