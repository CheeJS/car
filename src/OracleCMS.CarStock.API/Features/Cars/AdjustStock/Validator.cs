using FastEndpoints;
using FluentValidation;

namespace OracleCMS.CarStock.API.Features.Cars.AdjustStock;

public sealed class AdjustStockRequestValidator : Validator<AdjustStockRouteRequest>
{
    public AdjustStockRequestValidator()
    {
        RuleFor(x => x.Delta)
            .InclusiveBetween(-1_000_000, 1_000_000)
            .WithMessage("Delta must be between -1,000,000 and 1,000,000.");
    }
}
