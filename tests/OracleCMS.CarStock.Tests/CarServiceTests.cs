using OracleCMS.CarStock.API.Repositories;
using OracleCMS.CarStock.API.Services;

namespace OracleCMS.CarStock.Tests;

public sealed class CarServiceTests
{
    private static async Task<(int DealerA, int DealerB)> SeedDealersAsync(TestDatabase db)
    {
        var dealers = new DealerRepository(db.Factory);
        var a = await dealers.CreateAsync("a@example.com", "hash-a");
        var b = await dealers.CreateAsync("b@example.com", "hash-b");
        return (a, b);
    }

    private static CarService BuildService(TestDatabase db) => new(new CarRepository(db.Factory));

    [Fact]
    public async Task Add_PersistsCar_WithDealerId()
    {
        using var db = new TestDatabase();
        var (dealerA, _) = await SeedDealersAsync(db);
        var cars = BuildService(db);

        var result = await cars.AddAsync(dealerA, "Audi", "A4", 2018, 5);

        Assert.True(result.Car.Id > 0);
        Assert.Equal(dealerA, result.Car.DealerId);
        Assert.Equal("Audi", result.Car.Make);
        Assert.Equal("A4", result.Car.Model);
        Assert.Equal(2018, result.Car.Year);
        Assert.Equal(5, result.Car.Stock);
    }

    [Fact]
    public async Task Add_TrimsWhitespaceFromMakeAndModel()
    {
        using var db = new TestDatabase();
        var (dealerA, _) = await SeedDealersAsync(db);
        var cars = BuildService(db);

        var result = await cars.AddAsync(dealerA, "  Audi  ", " A4 ", 2018, 5);

        Assert.Equal("Audi", result.Car.Make);
        Assert.Equal("A4", result.Car.Model);
    }

    [Fact]
    public async Task GetById_OtherDealersCar_ReturnsNull()
    {
        using var db = new TestDatabase();
        var (dealerA, dealerB) = await SeedDealersAsync(db);
        var cars = BuildService(db);
        var car = await cars.AddAsync(dealerA, "Audi", "A4", 2018, 5);

        var result = await cars.GetByIdAsync(dealerB, car.Car.Id);

        Assert.Null(result);
    }

    [Fact]
    public async Task Delete_OtherDealersCar_ReturnsFalse_AndCarStillExists()
    {
        using var db = new TestDatabase();
        var (dealerA, dealerB) = await SeedDealersAsync(db);
        var cars = BuildService(db);
        var car = await cars.AddAsync(dealerA, "Audi", "A4", 2018, 5);

        var deleted = await cars.DeleteAsync(dealerB, car.Car.Id);

        Assert.False(deleted);
        var stillThere = await cars.GetByIdAsync(dealerA, car.Car.Id);
        Assert.NotNull(stillThere);
    }

    [Fact]
    public async Task UpdateStock_OtherDealersCar_ReturnsNull_AndStockUnchanged()
    {
        using var db = new TestDatabase();
        var (dealerA, dealerB) = await SeedDealersAsync(db);
        var cars = BuildService(db);
        var car = await cars.AddAsync(dealerA, "Audi", "A4", 2018, 5);

        var attempt = await cars.UpdateStockAsync(dealerB, car.Car.Id, 999);

        Assert.Null(attempt);
        var refreshed = await cars.GetByIdAsync(dealerA, car.Car.Id);
        Assert.NotNull(refreshed);
        Assert.Equal(5, refreshed!.Stock);
    }

    [Fact]
    public async Task UpdateStock_OwnCar_UpdatesAndReturnsRefreshedEntity()
    {
        using var db = new TestDatabase();
        var (dealerA, _) = await SeedDealersAsync(db);
        var cars = BuildService(db);
        var car = await cars.AddAsync(dealerA, "Audi", "A4", 2018, 5);

        var updated = await cars.UpdateStockAsync(dealerA, car.Car.Id, 12);

        Assert.NotNull(updated);
        Assert.Equal(12, updated!.Stock);
        Assert.Equal(car.Car.Id, updated.Id);
    }

    [Fact]
    public async Task Delete_OwnCar_ReturnsTrue_AndCarIsGone()
    {
        using var db = new TestDatabase();
        var (dealerA, _) = await SeedDealersAsync(db);
        var cars = BuildService(db);
        var car = await cars.AddAsync(dealerA, "Audi", "A4", 2018, 5);

        var deleted = await cars.DeleteAsync(dealerA, car.Car.Id);

        Assert.True(deleted);
        var gone = await cars.GetByIdAsync(dealerA, car.Car.Id);
        Assert.Null(gone);
    }

    [Fact]
    public async Task Search_OnlyReturnsCallersCars()
    {
        using var db = new TestDatabase();
        var (dealerA, dealerB) = await SeedDealersAsync(db);
        var cars = BuildService(db);
        await cars.AddAsync(dealerA, "Audi", "A4", 2018, 5);
        await cars.AddAsync(dealerA, "BMW", "320i", 2020, 2);
        await cars.AddAsync(dealerB, "Toyota", "Corolla", 2019, 7);

        var aResults = await cars.SearchAsync(dealerA, null, null);
        var bResults = await cars.SearchAsync(dealerB, null, null);

        Assert.Equal(2, aResults.Count);
        Assert.All(aResults, c => Assert.Equal(dealerA, c.DealerId));
        Assert.Single(bResults);
        Assert.Equal(dealerB, bResults[0].DealerId);
    }

    [Fact]
    public async Task Search_MakeFilter_IsCaseInsensitivePartialMatch()
    {
        using var db = new TestDatabase();
        var (dealerA, _) = await SeedDealersAsync(db);
        var cars = BuildService(db);
        await cars.AddAsync(dealerA, "Audi", "A4", 2018, 5);
        await cars.AddAsync(dealerA, "Audi", "A6", 2020, 2);
        await cars.AddAsync(dealerA, "BMW", "320i", 2022, 3);

        var results = await cars.SearchAsync(dealerA, "aud", null);

        Assert.Equal(2, results.Count);
        Assert.All(results, c => Assert.Equal("Audi", c.Make));
    }

    [Fact]
    public async Task Search_ModelFilter_IsCaseInsensitivePartialMatch()
    {
        using var db = new TestDatabase();
        var (dealerA, _) = await SeedDealersAsync(db);
        var cars = BuildService(db);
        await cars.AddAsync(dealerA, "Audi", "A4", 2018, 5);
        await cars.AddAsync(dealerA, "Audi", "A6", 2020, 2);
        await cars.AddAsync(dealerA, "BMW", "320i", 2022, 3);

        var results = await cars.SearchAsync(dealerA, null, "a");

        Assert.Equal(2, results.Count);
        Assert.All(results, c => Assert.Equal("Audi", c.Make));
    }

    [Fact]
    public async Task Search_CombinedMakeAndModel_IntersectsFilters()
    {
        using var db = new TestDatabase();
        var (dealerA, _) = await SeedDealersAsync(db);
        var cars = BuildService(db);
        await cars.AddAsync(dealerA, "Audi", "A4", 2018, 5);
        await cars.AddAsync(dealerA, "Audi", "A6", 2020, 2);

        var results = await cars.SearchAsync(dealerA, "audi", "a4");

        Assert.Single(results);
        Assert.Equal("A4", results[0].Model);
    }

    [Fact]
    public async Task Search_BlankFilters_TreatedAsAbsent()
    {
        using var db = new TestDatabase();
        var (dealerA, _) = await SeedDealersAsync(db);
        var cars = BuildService(db);
        await cars.AddAsync(dealerA, "Audi", "A4", 2018, 5);

        var results = await cars.SearchAsync(dealerA, "   ", "");

        Assert.Single(results);
    }
}
