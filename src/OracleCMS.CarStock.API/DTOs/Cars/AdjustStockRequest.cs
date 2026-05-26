using System.ComponentModel.DataAnnotations;

namespace OracleCMS.CarStock.API.DTOs.Cars;

public sealed class AdjustStockRequest
{
    [Required(ErrorMessage = "Delta is required.")]
    [Range(-1_000_000, 1_000_000, ErrorMessage = "Delta must be between -1,000,000 and 1,000,000.")]
    public int Delta { get; set; }
}
