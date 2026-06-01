using FastEndpoints;
using OracleCMS.CarStock.API.DTOs.Cars;
using OracleCMS.CarStock.API.Services.Interfaces;

namespace OracleCMS.CarStock.API.Features.Cars.GetById;

public sealed class GetCarByIdRequest
{
    public int Id { get; set; }
}

public sealed class GetCarByIdEndpoint : Endpoint<GetCarByIdRequest, CarResponse>
{
    private readonly ICarService _cars;

    public GetCarByIdEndpoint(ICarService cars)
    {
        _cars = cars;
    }

    public override void Configure()
    {
        Get("/api/cars/{id:int}");
        Description(d => d
            .Produces<CarResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithTags("Cars"));
        Summary(s =>
        {
            s.Summary = "Fetch a single car the caller owns.";
            s.Responses[200] = "Car found.";
            s.Responses[401] = "Missing or invalid token.";
            s.Responses[404] = "Car does not exist or belongs to another dealer.";
        });
    }

    public override async Task HandleAsync(GetCarByIdRequest req, CancellationToken ct)
    {
        var dealerId = User.GetDealerId();
        var car = await _cars.GetByIdAsync(dealerId, req.Id, ct);

        if (car is null)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status404NotFound;
            await HttpContext.Response.WriteAsJsonAsync(NotFoundBody.Value, ct);
            return;
        }

        await Send.OkAsync(CarResponse.FromEntity(car), ct);
    }
}
