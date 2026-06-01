using System.Security.Claims;

namespace OracleCMS.CarStock.API.Features.Cars;

internal static class DealerClaimExtensions
{
    public static int GetDealerId(this ClaimsPrincipal user)
    {
        var claim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? user.FindFirst("sub")?.Value
            ?? throw new UnauthorizedAccessException("Dealer ID claim missing.");
        if (!int.TryParse(claim, out var dealerId))
            throw new UnauthorizedAccessException("Dealer ID claim is not a valid integer.");
        return dealerId;
    }
}
