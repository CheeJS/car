namespace OracleCMS.CarStock.API.DTOs.Auth;

public sealed class AuthResponse
{
    public string Token { get; set; } = string.Empty;
}

public sealed class RegisterResponse
{
    public string Message { get; set; } = string.Empty;
}
