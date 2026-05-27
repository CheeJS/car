using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OracleCMS.CarStock.API.DTOs.Cars;
using OracleCMS.CarStock.API.Services.Interfaces;

namespace OracleCMS.CarStock.API.Controllers;

/// <summary>Manage cars and stock levels for the authenticated dealer.</summary>
/// <remarks>
/// All endpoints require a valid JWT and operate only on cars owned by the
/// caller — cross-dealer access returns <c>404 Not Found</c> rather than 403
/// so resource existence is never leaked to unauthorized parties.
/// </remarks>
[ApiController]
[Authorize]
[Route("api/cars")]
[Produces("application/json")]
public sealed class CarsController : ControllerBase
{
    private static readonly object NotFoundBody = new
    {
        error = "Car not found",
        detail = "No car with the given id exists for this dealer."
    };

    private readonly ICarService _cars;

    public CarsController(ICarService cars)
    {
        _cars = cars;
    }

    /// <summary>List cars belonging to the caller, optionally filtered by make and/or model.</summary>
    /// <param name="make">Partial, case-insensitive make filter (e.g. <c>audi</c> matches <c>Audi</c>).</param>
    /// <param name="model">Partial, case-insensitive model filter.</param>
    /// <response code="200">Filtered list (may be empty).</response>
    /// <response code="401">Missing or invalid token.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<CarResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] string? make,
        [FromQuery] string? model,
        CancellationToken cancellationToken)
    {
        var dealerId = GetDealerId();
        var cars = await _cars.SearchAsync(dealerId, make, model, cancellationToken);
        return Ok(cars.Select(CarResponse.FromEntity));
    }

    /// <summary>Add a new car to the caller's inventory.</summary>
    /// <response code="201">Car created; <c>Location</c> header set to the new resource.</response>
    /// <response code="400">Validation failed.</response>
    [HttpPost]
    [ProducesResponseType(typeof(CarResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Add(
        [FromBody] AddCarRequest request, CancellationToken cancellationToken)
    {
        if (!IsYearInRange(request.Year))
        {
            return BadRequest(new
            {
                error = "Validation failed",
                detail = $"Year must be between 1886 and {DateTime.UtcNow.Year + 1}."
            });
        }

        var dealerId = GetDealerId();
        var result = await _cars.AddAsync(
            dealerId, request.Make, request.Model, request.Year, request.Stock, cancellationToken);

        var response = CarResponse.FromEntity(result.Car);
        return CreatedAtAction(nameof(GetById), new { id = response.Id }, response);
    }

    /// <summary>Fetch a single car the caller owns.</summary>
    /// <response code="200">Car found.</response>
    /// <response code="404">Car does not exist or belongs to another dealer.</response>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(CarResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        [FromRoute] int id, CancellationToken cancellationToken)
    {
        var dealerId = GetDealerId();
        var car = await _cars.GetByIdAsync(dealerId, id, cancellationToken);
        if (car is null) return NotFound(NotFoundBody);
        return Ok(CarResponse.FromEntity(car));
    }

    /// <summary>Delete a car from the caller's inventory.</summary>
    /// <response code="204">Deleted.</response>
    /// <response code="404">Car does not exist or belongs to another dealer.</response>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        [FromRoute] int id, CancellationToken cancellationToken)
    {
        var dealerId = GetDealerId();
        var deleted = await _cars.DeleteAsync(dealerId, id, cancellationToken);
        if (!deleted) return NotFound(NotFoundBody);
        return NoContent();
    }

    /// <summary>Overwrite a car's stock level.</summary>
    /// <remarks>Use <c>PATCH /api/cars/{id}/stock/adjust</c> for relative updates that are safe under concurrent writes.</remarks>
    /// <response code="200">Updated; new stock reflected in the response.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="404">Car does not exist or belongs to another dealer.</response>
    [HttpPatch("{id:int}/stock")]
    [ProducesResponseType(typeof(CarResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStock(
        [FromRoute] int id,
        [FromBody] UpdateStockRequest request,
        CancellationToken cancellationToken)
    {
        var dealerId = GetDealerId();
        var car = await _cars.UpdateStockAsync(dealerId, id, request.Stock, cancellationToken);
        if (car is null) return NotFound(NotFoundBody);
        return Ok(CarResponse.FromEntity(car));
    }

    /// <summary>Adjust stock by a relative delta (e.g. <c>+1</c> on sale, <c>-1</c> on return).</summary>
    /// <remarks>
    /// The update is performed in a single SQL statement
    /// (<c>UPDATE … SET Stock = Stock + @Delta WHERE … AND Stock + @Delta >= 0</c>)
    /// so concurrent adjustments compose correctly without a read-then-write race window.
    /// Returns 400 if the resulting stock would be negative.
    /// </remarks>
    /// <response code="200">Adjusted; the response carries the new stock.</response>
    /// <response code="400">Validation failed, or the resulting stock would be negative.</response>
    /// <response code="404">Car does not exist or belongs to another dealer.</response>
    [HttpPatch("{id:int}/stock/adjust")]
    [ProducesResponseType(typeof(CarResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AdjustStock(
        [FromRoute] int id,
        [FromBody] AdjustStockRequest request,
        CancellationToken cancellationToken)
    {
        var dealerId = GetDealerId();
        var result = await _cars.AdjustStockAsync(dealerId, id, request.Delta, cancellationToken);

        return result.Status switch
        {
            AdjustStockStatus.Updated => Ok(CarResponse.FromEntity(result.Car!)),
            AdjustStockStatus.NotFound => NotFound(NotFoundBody),
            AdjustStockStatus.WouldGoNegative => BadRequest(new
            {
                error = "Invalid stock adjustment",
                detail = "Applying this delta would take stock below zero."
            }),
            _ => throw new InvalidOperationException($"Unhandled adjust status: {result.Status}.")
        };
    }

    private int GetDealerId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value
            ?? throw new UnauthorizedAccessException("Dealer ID claim missing.");
        if (!int.TryParse(claim, out var dealerId))
            throw new UnauthorizedAccessException("Dealer ID claim is not a valid integer.");
        return dealerId;
    }

    private static bool IsYearInRange(int year)
    {
        var max = DateTime.UtcNow.Year + 1;
        return year >= 1886 && year <= max;
    }
}
