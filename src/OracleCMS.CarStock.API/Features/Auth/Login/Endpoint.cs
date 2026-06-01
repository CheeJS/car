using FastEndpoints;
using OracleCMS.CarStock.API.DTOs.Auth;
using OracleCMS.CarStock.API.Services.Interfaces;

namespace OracleCMS.CarStock.API.Features.Auth.Login;

public sealed class LoginEndpoint : Endpoint<LoginRequest, AuthResponse>
{
    private readonly IAuthService _auth;

    public LoginEndpoint(IAuthService auth)
    {
        _auth = auth;
    }

    public override void Configure()
    {
        Post("/api/auth/login");
        AllowAnonymous();
        Options(b => b.RequireRateLimiting("auth"));
        Description(d => d
            .Produces<AuthResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .WithTags("Auth"));
        Summary(s =>
        {
            s.Summary = "Exchange credentials for a JWT bearer token.";
            s.Description =
                "On success returns a signed JWT with a 60-minute lifetime (configurable via " +
                "Jwt:ExpiryMinutes). Wrong password and unknown email both return the same 401 " +
                "response to prevent account enumeration.";
            s.Responses[200] = "Authenticated; token returned.";
            s.Responses[400] = "Validation failed.";
            s.Responses[401] = "Invalid credentials.";
            s.Responses[429] = "Too many requests (rate limited).";
        });
    }

    public override async Task HandleAsync(LoginRequest req, CancellationToken ct)
    {
        var token = await _auth.LoginAsync(req.Email, req.Password, ct);

        if (token is null)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await HttpContext.Response.WriteAsJsonAsync(new
            {
                error = "Invalid credentials",
                detail = "Email or password is incorrect."
            }, ct);
            return;
        }

        await Send.OkAsync(new AuthResponse { Token = token }, ct);
    }
}
