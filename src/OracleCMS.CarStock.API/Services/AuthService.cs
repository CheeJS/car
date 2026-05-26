using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OracleCMS.CarStock.API.Repositories.Interfaces;
using OracleCMS.CarStock.API.Services.Interfaces;

namespace OracleCMS.CarStock.API.Services;

public sealed class JwtOptions
{
    public string Secret { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public int ExpiryMinutes { get; set; } = 60;
}

public sealed class AuthService : IAuthService
{
    private const int BCryptWorkFactor = 12;

    private readonly IDealerRepository _dealers;
    private readonly JwtOptions _jwt;

    public AuthService(IDealerRepository dealers, IOptions<JwtOptions> jwt)
    {
        _dealers = dealers;
        _jwt = jwt.Value;
    }

    public async Task<RegisterOutcome> RegisterAsync(
        string email, string password, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.Trim();

        if (await _dealers.EmailExistsAsync(normalizedEmail, cancellationToken))
        {
            return RegisterOutcome.EmailAlreadyRegistered;
        }

        var hash = BCrypt.Net.BCrypt.HashPassword(password, BCryptWorkFactor);
        await _dealers.CreateAsync(normalizedEmail, hash, cancellationToken);
        return RegisterOutcome.Created;
    }

    public async Task<string?> LoginAsync(
        string email, string password, CancellationToken cancellationToken = default)
    {
        var dealer = await _dealers.GetByEmailAsync(email.Trim(), cancellationToken);
        if (dealer is null) return null;

        if (!BCrypt.Net.BCrypt.Verify(password, dealer.Password)) return null;

        return IssueToken(dealer.Id, dealer.Email);
    }

    private string IssueToken(int dealerId, string email)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, dealerId.ToString()),
            new Claim(ClaimTypes.NameIdentifier, dealerId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(_jwt.ExpiryMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
