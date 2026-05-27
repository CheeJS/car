using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace OracleCMS.CarStock.API.Validation;

public sealed class PasswordComplexityAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not string password || string.IsNullOrEmpty(password))
            return ValidationResult.Success;

        bool hasUpper   = Regex.IsMatch(password, @"[A-Z]");
        bool hasLower   = Regex.IsMatch(password, @"[a-z]");
        bool hasDigit   = Regex.IsMatch(password, @"[0-9]");
        bool hasSpecial = Regex.IsMatch(password, @"[!@#$%^&*()_+\-=\[\]{};':"",./<>?\\|]");

        if (hasUpper && hasLower && hasDigit && hasSpecial)
            return ValidationResult.Success;

        return new ValidationResult(
            "Password must contain at least one uppercase letter (A-Z), one lowercase letter (a-z), one digit (0-9), and one special character (!@#$%^&*()_+-=[]{};\':\",./<>?\\|).");
    }
}
