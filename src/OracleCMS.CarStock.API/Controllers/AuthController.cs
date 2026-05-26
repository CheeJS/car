using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using OracleCMS.CarStock.API.DTOs.Auth;
using OracleCMS.CarStock.API.Services.Interfaces;

namespace OracleCMS.CarStock.API.Controllers;

/// <summary>Dealer registration and authentication.</summary>
[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _auth;

    public AuthController(IAuthService auth)
    {
        _auth = auth;
    }

    /// <summary>Register a new dealer account.</summary>
    /// <remarks>
    /// Passwords are hashed with BCrypt (work factor 12) before storage.
    /// Email matching is case-insensitive, so "Dealer@x.com" and "dealer@x.com"
    /// cannot both be registered.
    /// </remarks>
    /// <response code="201">Dealer created.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="409">Email already registered.</response>
    /// <response code="429">Too many requests (rate limited).</response>
    [HttpPost("register")]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(RegisterResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
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

    /// <summary>Exchange credentials for a JWT bearer token.</summary>
    /// <remarks>
    /// On success returns a signed JWT with a 60-minute lifetime (configurable via
    /// <c>Jwt:ExpiryMinutes</c>). Wrong password and unknown email both return the
    /// same 401 response to prevent account enumeration.
    /// </remarks>
    /// <response code="200">Authenticated; token returned.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="401">Invalid credentials.</response>
    /// <response code="429">Too many requests (rate limited).</response>
    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
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

    /// <summary>Return the current dealer's profile, derived from the JWT.</summary>
    /// <remarks>
    /// Useful for verifying that a token is valid and for clients that need to
    /// display the logged-in dealer without round-tripping the email on every request.
    /// </remarks>
    /// <response code="200">Profile returned.</response>
    /// <response code="401">Missing or invalid token.</response>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(DealerProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult Me()
    {
        var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        var emailClaim = User.FindFirst(ClaimTypes.Email)?.Value
            ?? User.FindFirst("email")?.Value
            ?? string.Empty;

        if (string.IsNullOrEmpty(idClaim) || !int.TryParse(idClaim, out var id))
        {
            return Unauthorized(new
            {
                error = "Invalid token",
                detail = "Dealer identifier claim is missing or invalid."
            });
        }

        return Ok(new DealerProfileResponse { Id = id, Email = emailClaim });
    }
}
