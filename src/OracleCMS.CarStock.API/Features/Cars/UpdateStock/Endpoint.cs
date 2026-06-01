using FastEndpoints;
using OracleCMS.CarStock.API.DTOs.Cars;
using OracleCMS.CarStock.API.Services.Interfaces;

namespace OracleCMS.CarStock.API.Features.Cars.UpdateStock;

public sealed class UpdateStockRouteRequest
{
    public int Id { get; set; }
    public int Stock { get; set; }
}

public sealed class UpdateStockEndpoint : Endpoint<UpdateStockRouteRequest, CarResponse>
{
    private readonly ICarService _cars;

    public UpdateStockEndpoint(ICarService cars)
    {
        _cars = cars;
    }

    public override void Configure()
    {
        Patch("/api/cars/{id:int}/stock");
        Description(d => d
            .Produces<CarResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithTags("Cars"));
        Summary(s =>
        {
            s.Summary = "Overwrite a car's stock level.";
            s.Description =
                "Use PATCH /api/cars/{id}/stock/adjust for relative updates that are safe under concurrent writes.";
            s.Responses[200] = "Updated; new stock reflected in the response.";
            s.Responses[400] = "Validation failed.";
            s.Responses[401] = "Missing or invalid token.";
            s.Responses[404] = "Car does not exist or belongs to another dealer.";
        });
    }

    public override async Task HandleAsync(UpdateStockRouteRequest req, CancellationToken ct)
    {
        var dealerId = User.GetDealerId();
        var car = await _cars.UpdateStockAsync(dealerId, req.Id, req.Stock, ct);

        if (car is null)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status404NotFound;
            await HttpContext.Response.WriteAsJsonAsync(NotFoundBody.Value, ct);
            return;
        }

        await Send.OkAsync(CarResponse.FromEntity(car), ct);
    }
}
