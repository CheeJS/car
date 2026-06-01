using FastEndpoints;
using OracleCMS.CarStock.API.DTOs.Cars;
using OracleCMS.CarStock.API.Services.Interfaces;

namespace OracleCMS.CarStock.API.Features.Cars.List;

public sealed class ListCarsRequest
{
    public string? Make { get; set; }
    public string? Model { get; set; }
}

public sealed class ListCarsEndpoint : Endpoint<ListCarsRequest, IEnumerable<CarResponse>>
{
    private readonly ICarService _cars;

    public ListCarsEndpoint(ICarService cars)
    {
        _cars = cars;
    }

    public override void Configure()
    {
        Get("/api/cars");
        Description(d => d
            .Produces<IEnumerable<CarResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithTags("Cars"));
        Summary(s =>
        {
            s.Summary = "List cars belonging to the caller, optionally filtered by make and/or model.";
            s.Description =
                "Both filters are partial and case-insensitive. e.g. make=audi matches Audi.";
            s.Responses[200] = "Filtered list (may be empty).";
            s.Responses[401] = "Missing or invalid token.";
        });
    }

    public override async Task HandleAsync(ListCarsRequest req, CancellationToken ct)
    {
        var dealerId = User.GetDealerId();
        var cars = await _cars.SearchAsync(dealerId, req.Make, req.Model, ct);
        await Send.OkAsync(cars.Select(CarResponse.FromEntity), ct);
    }
}
