using FastEndpoints;
using OracleCMS.CarStock.API.Services.Interfaces;

namespace OracleCMS.CarStock.API.Features.Cars.Delete;

public sealed class DeleteCarRequest
{
    public int Id { get; set; }
}

public sealed class DeleteCarEndpoint : Endpoint<DeleteCarRequest>
{
    private readonly ICarService _cars;

    public DeleteCarEndpoint(ICarService cars)
    {
        _cars = cars;
    }

    public override void Configure()
    {
        Delete("/api/cars/{id:int}");
        Description(d => d
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithTags("Cars"));
        Summary(s =>
        {
            s.Summary = "Delete a car from the caller's inventory.";
            s.Responses[204] = "Deleted.";
            s.Responses[401] = "Missing or invalid token.";
            s.Responses[404] = "Car does not exist or belongs to another dealer.";
        });
    }

    public override async Task HandleAsync(DeleteCarRequest req, CancellationToken ct)
    {
        var dealerId = User.GetDealerId();
        var deleted = await _cars.DeleteAsync(dealerId, req.Id, ct);

        if (!deleted)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status404NotFound;
            await HttpContext.Response.WriteAsJsonAsync(NotFoundBody.Value, ct);
            return;
        }

        await Send.NoContentAsync(ct);
    }
}
