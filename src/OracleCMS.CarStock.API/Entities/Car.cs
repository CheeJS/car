namespace OracleCMS.CarStock.API.Entities;

public sealed class Car
{
    public int Id { get; set; }
    public int DealerId { get; set; }
    public string Make { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int Year { get; set; }
    public int Stock { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
