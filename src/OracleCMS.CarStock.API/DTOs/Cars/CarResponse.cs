using OracleCMS.CarStock.API.Entities;

namespace OracleCMS.CarStock.API.DTOs.Cars;

public sealed class CarResponse
{
    public int Id { get; set; }
    public string Make { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int Year { get; set; }
    public int Stock { get; set; }

    public static CarResponse FromEntity(Car car) => new()
    {
        Id = car.Id,
        Make = car.Make,
        Model = car.Model,
        Year = car.Year,
        Stock = car.Stock
    };
}
