using System.Security.Claims;
using FastEndpoints;
using OracleCMS.CarStock.API.DTOs.Auth;

namespace OracleCMS.CarStock.API.Features.Auth.Me;

public sealed class MeEndpoint : EndpointWithoutRequest<DealerProfileResponse>
{
    public override void Configure()
    {
        Get("/api/auth/me");
        Description(d => d
            .Produces<DealerProfileResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithTags("Auth"));
        Summary(s =>
        {
            s.Summary = "Return the current dealer's profile, derived from the JWT.";
            s.Description =
                "Useful for verifying that a token is valid and for clients that need to " +
                "display the logged-in dealer without round-tripping the email on every request.";
            s.Responses[200] = "Profile returned.";
            s.Responses[401] = "Missing or invalid token.";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        var emailClaim = User.FindFirst(ClaimTypes.Email)?.Value
            ?? User.FindFirst("email")?.Value
            ?? string.Empty;

        if (string.IsNullOrEmpty(idClaim) || !int.TryParse(idClaim, out var id))
        {
            HttpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await HttpContext.Response.WriteAsJsonAsync(new
            {
                error = "Invalid token",
                detail = "Dealer identifier claim is missing or invalid."
            }, ct);
            return;
        }

        await Send.OkAsync(new DealerProfileResponse { Id = id, Email = emailClaim }, ct);
    }
}
