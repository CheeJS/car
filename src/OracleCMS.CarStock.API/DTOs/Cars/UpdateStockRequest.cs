using System.ComponentModel.DataAnnotations;

namespace OracleCMS.CarStock.API.DTOs.Cars;

public sealed class UpdateStockRequest
{
    [Required(ErrorMessage = "Stock is required.")]
    [Range(0, int.MaxValue, ErrorMessage = "Stock must be zero or greater.")]
    public int Stock { get; set; }
}
