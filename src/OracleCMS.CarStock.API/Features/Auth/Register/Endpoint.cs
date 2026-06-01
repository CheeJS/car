using FastEndpoints;
using OracleCMS.CarStock.API.DTOs.Auth;
using OracleCMS.CarStock.API.Services.Interfaces;

namespace OracleCMS.CarStock.API.Features.Auth.Register;

public sealed class RegisterEndpoint : Endpoint<RegisterRequest, RegisterResponse>
{
    private readonly IAuthService _auth;

    public RegisterEndpoint(IAuthService auth)
    {
        _auth = auth;
    }

    public override void Configure()
    {
        Post("/api/auth/register");
        AllowAnonymous();
        Options(b => b.RequireRateLimiting("auth"));
        Description(d => d
            .Produces<RegisterResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .WithTags("Auth"));
        Summary(s =>
        {
            s.Summary = "Register a new dealer account.";
            s.Description =
                "Passwords are hashed with BCrypt (work factor 12) before storage. " +
                "Email matching is case-insensitive, so \"Dealer@x.com\" and \"dealer@x.com\" " +
                "cannot both be registered.";
            s.Responses[201] = "Dealer created.";
            s.Responses[400] = "Validation failed.";
            s.Responses[409] = "Email already registered.";
            s.Responses[429] = "Too many requests (rate limited).";
        });
    }

    public override async Task HandleAsync(RegisterRequest req, CancellationToken ct)
    {
        var outcome = await _auth.RegisterAsync(req.Email, req.Password, ct);

        switch (outcome)
        {
            case RegisterOutcome.Created:
                await Send.ResponseAsync(
                    new RegisterResponse { Message = "Dealer registered successfully." },
                    StatusCodes.Status201Created,
                    ct);
                return;

            case RegisterOutcome.EmailAlreadyRegistered:
                HttpContext.Response.StatusCode = StatusCodes.Status409Conflict;
                await HttpContext.Response.WriteAsJsonAsync(new
                {
                    error = "Email already registered",
                    detail = "An account with this email already exists."
                }, ct);
                return;

            default:
                throw new InvalidOperationException($"Unhandled register outcome: {outcome}.");
        }
    }
}
