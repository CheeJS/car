using FastEndpoints;
using OracleCMS.CarStock.API.DTOs.Cars;
using OracleCMS.CarStock.API.Features.Cars.GetById;
using OracleCMS.CarStock.API.Services.Interfaces;

namespace OracleCMS.CarStock.API.Features.Cars.Add;

public sealed class AddCarEndpoint : Endpoint<AddCarRequest, CarResponse>
{
    private readonly ICarService _cars;

    public AddCarEndpoint(ICarService cars)
    {
        _cars = cars;
    }

    public override void Configure()
    {
        Post("/api/cars");
        Description(d => d
            .Produces<CarResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithTags("Cars"));
        Summary(s =>
        {
            s.Summary = "Add a new car to the caller's inventory.";
            s.Responses[201] = "Car created; Location header set to the new resource.";
            s.Responses[400] = "Validation failed.";
            s.Responses[401] = "Missing or invalid token.";
        });
    }

    public override async Task HandleAsync(AddCarRequest req, CancellationToken ct)
    {
        if (!IsYearInRange(req.Year))
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await HttpContext.Response.WriteAsJsonAsync(new
            {
                error = "Validation failed",
                detail = $"Year must be between 1886 and {DateTime.UtcNow.Year + 1}."
            }, ct);
            return;
        }

        var dealerId = User.GetDealerId();
        var result = await _cars.AddAsync(dealerId, req.Make, req.Model, req.Year, req.Stock, ct);
        var response = CarResponse.FromEntity(result.Car);

        await Send.CreatedAtAsync<GetCarByIdEndpoint>(
            new { id = response.Id },
            response,
            cancellation: ct);
    }

    private static bool IsYearInRange(int year)
    {
        var max = DateTime.UtcNow.Year + 1;
        return year >= 1886 && year <= max;
    }
}
