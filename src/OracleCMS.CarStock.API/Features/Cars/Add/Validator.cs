using FastEndpoints;
using FluentValidation;
using OracleCMS.CarStock.API.DTOs.Cars;

namespace OracleCMS.CarStock.API.Features.Cars.Add;

public sealed class AddCarRequestValidator : Validator<AddCarRequest>
{
    public AddCarRequestValidator()
    {
        RuleFor(x => x.Make)
            .NotEmpty().WithMessage("Make is required.")
            .Length(1, 100).WithMessage("Make must be between 1 and 100 characters.");

        RuleFor(x => x.Model)
            .NotEmpty().WithMessage("Model is required.")
            .Length(1, 100).WithMessage("Model must be between 1 and 100 characters.");

        RuleFor(x => x.Year)
            .InclusiveBetween(1886, 9999).WithMessage("Year must be between 1886 and next year.");

        RuleFor(x => x.Stock)
            .GreaterThanOrEqualTo(0).WithMessage("Stock must be zero or greater.");
    }
}
