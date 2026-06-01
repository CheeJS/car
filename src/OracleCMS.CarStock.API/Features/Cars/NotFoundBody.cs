namespace OracleCMS.CarStock.API.Features.Cars;

internal static class NotFoundBody
{
    public static readonly object Value = new
    {
        error = "Car not found",
        detail = "No car with the given id exists for this dealer."
    };
}
