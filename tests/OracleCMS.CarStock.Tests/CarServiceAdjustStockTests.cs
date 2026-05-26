using OracleCMS.CarStock.API.Repositories;
using OracleCMS.CarStock.API.Services;
using OracleCMS.CarStock.API.Services.Interfaces;

namespace OracleCMS.CarStock.Tests;

public sealed class CarServiceAdjustStockTests
{
    private static async Task<(int DealerA, int DealerB)> SeedDealersAsync(TestDatabase db)
    {
        var dealers = new DealerRepository(db.Factory);
        return (await dealers.CreateAsync("a@example.com", "hash-a"),
                await dealers.CreateAsync("b@example.com", "hash-b"));
    }

    private static CarService BuildService(TestDatabase db) => new(new CarRepository(db.Factory));

    [Fact]
    public async Task Adjust_PositiveDelta_IncrementsStock()
    {
        using var db = new TestDatabase();
        var (dealerA, _) = await SeedDealersAsync(db);
        var cars = BuildService(db);
        var car = await cars.AddAsync(dealerA, "Audi", "A4", 2018, 5);

        var result = await cars.AdjustStockAsync(dealerA, car.Car.Id, +3);

        Assert.Equal(AdjustStockStatus.Updated, result.Status);
        Assert.NotNull(result.Car);
        Assert.Equal(8, result.Car!.Stock);
    }

    [Fact]
    public async Task Adjust_NegativeDelta_DecrementsStock()
    {
        using var db = new TestDatabase();
        var (dealerA, _) = await SeedDealersAsync(db);
        var cars = BuildService(db);
        var car = await cars.AddAsync(dealerA, "Audi", "A4", 2018, 5);

        var result = await cars.AdjustStockAsync(dealerA, car.Car.Id, -2);

        Assert.Equal(AdjustStockStatus.Updated, result.Status);
        Assert.Equal(3, result.Car!.Stock);
    }

    [Fact]
    public async Task Adjust_TakingBelowZero_ReturnsWouldGoNegative_AndStockUnchanged()
    {
        using var db = new TestDatabase();
        var (dealerA, _) = await SeedDealersAsync(db);
        var cars = BuildService(db);
        var car = await cars.AddAsync(dealerA, "Audi", "A4", 2018, 2);

        var result = await cars.AdjustStockAsync(dealerA, car.Car.Id, -5);

        Assert.Equal(AdjustStockStatus.WouldGoNegative, result.Status);
        var unchanged = await cars.GetByIdAsync(dealerA, car.Car.Id);
        Assert.Equal(2, unchanged!.Stock);
    }

    [Fact]
    public async Task Adjust_OtherDealersCar_ReturnsNotFound()
    {
        using var db = new TestDatabase();
        var (dealerA, dealerB) = await SeedDealersAsync(db);
        var cars = BuildService(db);
        var car = await cars.AddAsync(dealerA, "Audi", "A4", 2018, 5);

        var result = await cars.AdjustStockAsync(dealerB, car.Car.Id, +1);

        Assert.Equal(AdjustStockStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task Adjust_NonexistentCar_ReturnsNotFound()
    {
        using var db = new TestDatabase();
        var (dealerA, _) = await SeedDealersAsync(db);
        var cars = BuildService(db);

        var result = await cars.AdjustStockAsync(dealerA, 9999, +1);

        Assert.Equal(AdjustStockStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task Adjust_ExactlyToZero_Succeeds()
    {
        using var db = new TestDatabase();
        var (dealerA, _) = await SeedDealersAsync(db);
        var cars = BuildService(db);
        var car = await cars.AddAsync(dealerA, "Audi", "A4", 2018, 3);

        var result = await cars.AdjustStockAsync(dealerA, car.Car.Id, -3);

        Assert.Equal(AdjustStockStatus.Updated, result.Status);
        Assert.Equal(0, result.Car!.Stock);
    }
}
