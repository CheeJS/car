using System.Text.RegularExpressions;
using FastEndpoints;
using FluentValidation;
using OracleCMS.CarStock.API.DTOs.Auth;

namespace OracleCMS.CarStock.API.Features.Auth.Register;

public sealed class RegisterRequestValidator : Validator<RegisterRequest>
{
    private const string ComplexityMessage =
        "Password must contain at least one uppercase letter (A-Z), one lowercase letter (a-z), " +
        "one digit (0-9), and one special character (!@#$%^&*()_+-=[]{};':\",./<>?\\|).";

    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Email must be a valid email address.")
            .MaximumLength(254).WithMessage("Email cannot exceed 254 characters.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.")
            .MaximumLength(128).WithMessage("Password cannot exceed 128 characters.")
            .Must(HasFourCharacterClasses).WithMessage(ComplexityMessage);
    }

    private static bool HasFourCharacterClasses(string? password)
    {
        if (string.IsNullOrEmpty(password)) return true;

        return Regex.IsMatch(password, "[A-Z]")
            && Regex.IsMatch(password, "[a-z]")
            && Regex.IsMatch(password, "[0-9]")
            && Regex.IsMatch(password, @"[!@#$%^&*()_+\-=\[\]{};':"",./<>?\\|]");
    }
}
