using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OracleCMS.CarStock.API.DTOs.Cars;
using OracleCMS.CarStock.API.Services.Interfaces;

namespace OracleCMS.CarStock.API.Controllers;

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

    private int GetDealerId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value
            ?? throw new UnauthorizedAccessException("Dealer ID claim missing.");
        return int.Parse(claim);
    }

    private static bool IsYearInRange(int year)
    {
        var max = DateTime.UtcNow.Year + 1;
        return year >= 1886 && year <= max;
    }
}
