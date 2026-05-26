using System.ComponentModel.DataAnnotations;

namespace OracleCMS.CarStock.API.DTOs.Cars;

public sealed class AddCarRequest
{
    [Required(ErrorMessage = "Make is required.")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Make must be between 1 and 100 characters.")]
    public string Make { get; set; } = string.Empty;

    [Required(ErrorMessage = "Model is required.")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Model must be between 1 and 100 characters.")]
    public string Model { get; set; } = string.Empty;

    [Required(ErrorMessage = "Year is required.")]
    [Range(1886, 9999, ErrorMessage = "Year must be between 1886 and next year.")]
    public int Year { get; set; }

    [Required(ErrorMessage = "Stock is required.")]
    [Range(0, int.MaxValue, ErrorMessage = "Stock must be zero or greater.")]
    public int Stock { get; set; }
}
