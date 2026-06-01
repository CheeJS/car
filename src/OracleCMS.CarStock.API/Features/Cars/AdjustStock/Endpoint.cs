using FastEndpoints;
using OracleCMS.CarStock.API.DTOs.Cars;
using OracleCMS.CarStock.API.Services.Interfaces;

namespace OracleCMS.CarStock.API.Features.Cars.AdjustStock;

public sealed class AdjustStockRouteRequest
{
    public int Id { get; set; }
    public int Delta { get; set; }
}

public sealed class AdjustStockEndpoint : Endpoint<AdjustStockRouteRequest, CarResponse>
{
    private readonly ICarService _cars;

    public AdjustStockEndpoint(ICarService cars)
    {
        _cars = cars;
    }

    public override void Configure()
    {
        Patch("/api/cars/{id:int}/stock/adjust");
        Description(d => d
            .Produces<CarResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithTags("Cars"));
        Summary(s =>
        {
            s.Summary = "Adjust stock by a relative delta (e.g. +1 on sale, -1 on return).";
            s.Description =
                "The update is performed in a single SQL statement " +
                "(UPDATE … SET Stock = Stock + @Delta WHERE … AND Stock + @Delta >= 0) " +
                "so concurrent adjustments compose correctly without a read-then-write race window. " +
                "Returns 400 if the resulting stock would be negative.";
            s.Responses[200] = "Adjusted; the response carries the new stock.";
            s.Responses[400] = "Validation failed, or the resulting stock would be negative.";
            s.Responses[401] = "Missing or invalid token.";
            s.Responses[404] = "Car does not exist or belongs to another dealer.";
        });
    }

    public override async Task HandleAsync(AdjustStockRouteRequest req, CancellationToken ct)
    {
        var dealerId = User.GetDealerId();
        var result = await _cars.AdjustStockAsync(dealerId, req.Id, req.Delta, ct);

        switch (result.Status)
        {
            case AdjustStockStatus.Updated:
                await Send.OkAsync(CarResponse.FromEntity(result.Car!), ct);
                return;

            case AdjustStockStatus.NotFound:
                HttpContext.Response.StatusCode = StatusCodes.Status404NotFound;
                await HttpContext.Response.WriteAsJsonAsync(NotFoundBody.Value, ct);
                return;

            case AdjustStockStatus.WouldGoNegative:
                HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
                await HttpContext.Response.WriteAsJsonAsync(new
                {
                    error = "Invalid stock adjustment",
                    detail = "Applying this delta would take stock below zero."
                }, ct);
                return;

            default:
                throw new InvalidOperationException($"Unhandled adjust status: {result.Status}.");
        }
    }
}
