using FastEndpoints;
using FluentValidation;

namespace OracleCMS.CarStock.API.Features.Cars.UpdateStock;

public sealed class UpdateStockRequestValidator : Validator<UpdateStockRouteRequest>
{
    public UpdateStockRequestValidator()
    {
        RuleFor(x => x.Stock)
            .GreaterThanOrEqualTo(0).WithMessage("Stock must be zero or greater.");
    }
}
