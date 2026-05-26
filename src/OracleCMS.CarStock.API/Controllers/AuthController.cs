using Microsoft.AspNetCore.Mvc;
using OracleCMS.CarStock.API.DTOs.Auth;
using OracleCMS.CarStock.API.Services.Interfaces;

namespace OracleCMS.CarStock.API.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _auth;

    public AuthController(IAuthService auth)
    {
        _auth = auth;
    }

    [HttpPost("register")]
    [ProducesResponseType(typeof(RegisterResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        var outcome = await _auth.RegisterAsync(request.Email, request.Password, cancellationToken);

        return outcome switch
        {
            RegisterOutcome.Created => StatusCode(
                StatusCodes.Status201Created,
                new RegisterResponse { Message = "Dealer registered successfully." }),
            RegisterOutcome.EmailAlreadyRegistered => Conflict(new
            {
                error = "Email already registered",
                detail = "An account with this email already exists."
            }),
            _ => throw new InvalidOperationException($"Unhandled register outcome: {outcome}.")
        };
    }

    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var token = await _auth.LoginAsync(request.Email, request.Password, cancellationToken);
        if (token is null)
        {
            return Unauthorized(new
            {
                error = "Invalid credentials",
                detail = "Email or password is incorrect."
            });
        }

        return Ok(new AuthResponse { Token = token });
    }
}
