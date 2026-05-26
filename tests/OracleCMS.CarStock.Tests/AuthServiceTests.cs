using Microsoft.Extensions.Options;
using OracleCMS.CarStock.API.Repositories;
using OracleCMS.CarStock.API.Services;
using OracleCMS.CarStock.API.Services.Interfaces;

namespace OracleCMS.CarStock.Tests;

public sealed class AuthServiceTests
{
    private static AuthService BuildService(TestDatabase db)
    {
        var dealers = new DealerRepository(db.Factory);
        var options = Options.Create(new JwtOptions
        {
            Secret = "unit-test-secret-please-rotate-32chars-minimum-length",
            Issuer = "OracleCMS.CarStock.Tests",
            Audience = "OracleCMS.CarStock.Tests.Audience",
            ExpiryMinutes = 60
        });
        return new AuthService(dealers, options);
    }

    [Fact]
    public async Task Register_NewEmail_CreatesDealer()
    {
        using var db = new TestDatabase();
        var auth = BuildService(db);

        var outcome = await auth.RegisterAsync("dealer@example.com", "SecurePass1!");

        Assert.Equal(RegisterOutcome.Created, outcome);
    }

    [Fact]
    public async Task Register_DuplicateEmail_ReturnsEmailAlreadyRegistered()
    {
        using var db = new TestDatabase();
        var auth = BuildService(db);

        await auth.RegisterAsync("dealer@example.com", "SecurePass1!");
        var second = await auth.RegisterAsync("dealer@example.com", "SecurePass1!");

        Assert.Equal(RegisterOutcome.EmailAlreadyRegistered, second);
    }

    [Fact]
    public async Task Register_DuplicateEmailDifferentCase_ReturnsEmailAlreadyRegistered()
    {
        using var db = new TestDatabase();
        var auth = BuildService(db);

        await auth.RegisterAsync("Dealer@Example.com", "SecurePass1!");
        var second = await auth.RegisterAsync("dealer@example.com", "SecurePass1!");

        Assert.Equal(RegisterOutcome.EmailAlreadyRegistered, second);
    }

    [Fact]
    public async Task Login_CorrectCredentials_ReturnsToken()
    {
        using var db = new TestDatabase();
        var auth = BuildService(db);
        await auth.RegisterAsync("dealer@example.com", "SecurePass1!");

        var token = await auth.LoginAsync("dealer@example.com", "SecurePass1!");

        Assert.False(string.IsNullOrWhiteSpace(token));
        Assert.Equal(3, token!.Split('.').Length);
    }

    [Fact]
    public async Task Login_WrongPassword_ReturnsNull()
    {
        using var db = new TestDatabase();
        var auth = BuildService(db);
        await auth.RegisterAsync("dealer@example.com", "SecurePass1!");

        var token = await auth.LoginAsync("dealer@example.com", "WrongPassword");

        Assert.Null(token);
    }

    [Fact]
    public async Task Login_UnknownEmail_ReturnsNull()
    {
        using var db = new TestDatabase();
        var auth = BuildService(db);

        var token = await auth.LoginAsync("nobody@example.com", "anything");

        Assert.Null(token);
    }

    [Fact]
    public async Task Login_EmailIsCaseInsensitive()
    {
        using var db = new TestDatabase();
        var auth = BuildService(db);
        await auth.RegisterAsync("dealer@example.com", "SecurePass1!");

        var token = await auth.LoginAsync("DEALER@example.com", "SecurePass1!");

        Assert.NotNull(token);
    }

    [Fact]
    public async Task Register_StoresBcryptHash_NotPlaintext()
    {
        using var db = new TestDatabase();
        var auth = BuildService(db);
        var dealers = new DealerRepository(db.Factory);

        await auth.RegisterAsync("dealer@example.com", "SecurePass1!");
        var stored = await dealers.GetByEmailAsync("dealer@example.com");

        Assert.NotNull(stored);
        Assert.NotEqual("SecurePass1!", stored!.PasswordHash);
        Assert.StartsWith("$2", stored.PasswordHash);
    }
}
