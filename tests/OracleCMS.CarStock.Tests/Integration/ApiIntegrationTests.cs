using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace OracleCMS.CarStock.Tests.Integration;

public sealed class ApiIntegrationTests : IClassFixture<CarStockWebAppFactory>
{
    private readonly CarStockWebAppFactory _factory;

    public ApiIntegrationTests(CarStockWebAppFactory factory)
    {
        _factory = factory;
    }

    private HttpClient NewClient() => _factory.CreateClient();

    private static async Task<string> RegisterAndLoginAsync(
        HttpClient client, string email, string password)
    {
        var registerResponse = await client.PostAsJsonAsync(
            "/api/auth/register", new { email, password });
        registerResponse.EnsureSuccessStatusCode();

        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login", new { email, password });
        loginResponse.EnsureSuccessStatusCode();

        var body = await loginResponse.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        return json.RootElement.GetProperty("token").GetString()!;
    }

    private static void SetToken(HttpClient client, string token)
        => client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    [Fact]
    public async Task FullFlow_RegisterLoginAddListSearchPatchDelete()
    {
        var client = NewClient();
        var token = await RegisterAndLoginAsync(
            client, $"flow-{Guid.NewGuid():N}@example.com", "SecurePass1!");
        SetToken(client, token);

        var addResp = await client.PostAsJsonAsync("/api/cars",
            new { make = "Audi", model = "A4", year = 2018, stock = 5 });
        Assert.Equal(HttpStatusCode.Created, addResp.StatusCode);
        Assert.NotNull(addResp.Headers.Location);

        var addedJson = JsonDocument.Parse(await addResp.Content.ReadAsStringAsync());
        var carId = addedJson.RootElement.GetProperty("id").GetInt32();

        var list = await client.GetFromJsonAsync<JsonElement>("/api/cars");
        Assert.Equal(1, list.GetArrayLength());

        var search = await client.GetFromJsonAsync<JsonElement>("/api/cars?make=aud");
        Assert.Equal(1, search.GetArrayLength());

        var patchResp = await client.PatchAsJsonAsync(
            $"/api/cars/{carId}/stock", new { stock = 12 });
        patchResp.EnsureSuccessStatusCode();
        var patched = JsonDocument.Parse(await patchResp.Content.ReadAsStringAsync());
        Assert.Equal(12, patched.RootElement.GetProperty("stock").GetInt32());

        var deleteResp = await client.DeleteAsync($"/api/cars/{carId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResp.StatusCode);

        var afterDelete = await client.GetFromJsonAsync<JsonElement>("/api/cars");
        Assert.Equal(0, afterDelete.GetArrayLength());
    }

    [Fact]
    public async Task Unauthenticated_CarsRequest_Returns401()
    {
        var client = NewClient();

        var response = await client.GetAsync("/api/cars");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CrossDealer_GetByOther_Returns404()
    {
        var clientA = NewClient();
        var clientB = NewClient();
        var tokenA = await RegisterAndLoginAsync(
            clientA, $"a-{Guid.NewGuid():N}@example.com", "PasswordA1!");
        var tokenB = await RegisterAndLoginAsync(
            clientB, $"b-{Guid.NewGuid():N}@example.com", "PasswordB1!");
        SetToken(clientA, tokenA);
        SetToken(clientB, tokenB);

        var addResp = await clientA.PostAsJsonAsync("/api/cars",
            new { make = "Audi", model = "A4", year = 2018, stock = 5 });
        var carId = JsonDocument.Parse(await addResp.Content.ReadAsStringAsync())
            .RootElement.GetProperty("id").GetInt32();

        var getOther = await clientB.GetAsync($"/api/cars/{carId}");
        var deleteOther = await clientB.DeleteAsync($"/api/cars/{carId}");
        var patchOther = await clientB.PatchAsJsonAsync(
            $"/api/cars/{carId}/stock", new { stock = 999 });
        var adjustOther = await clientB.PatchAsJsonAsync(
            $"/api/cars/{carId}/stock/adjust", new { delta = -1 });

        Assert.Equal(HttpStatusCode.NotFound, getOther.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, deleteOther.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, patchOther.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, adjustOther.StatusCode);

        SetToken(clientA, tokenA);
        var stillThere = await clientA.GetFromJsonAsync<JsonElement>($"/api/cars/{carId}");
        Assert.Equal(5, stillThere.GetProperty("stock").GetInt32());
    }

    [Fact]
    public async Task Me_ReturnsCurrentDealerProfile()
    {
        var client = NewClient();
        var email = $"me-{Guid.NewGuid():N}@example.com";
        var token = await RegisterAndLoginAsync(client, email, "MePass1!");
        SetToken(client, token);

        var profile = await client.GetFromJsonAsync<JsonElement>("/api/auth/me");

        Assert.True(profile.GetProperty("id").GetInt32() > 0);
        Assert.Equal(email, profile.GetProperty("email").GetString());
    }

    [Fact]
    public async Task Me_WithoutToken_Returns401()
    {
        var client = NewClient();

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AdjustStock_PositiveDelta_IncrementsStock()
    {
        var client = NewClient();
        var token = await RegisterAndLoginAsync(
            client, $"adj-{Guid.NewGuid():N}@example.com", "AdjPass1!");
        SetToken(client, token);

        var addResp = await client.PostAsJsonAsync("/api/cars",
            new { make = "Audi", model = "A4", year = 2018, stock = 5 });
        var carId = JsonDocument.Parse(await addResp.Content.ReadAsStringAsync())
            .RootElement.GetProperty("id").GetInt32();

        var adjust = await client.PatchAsJsonAsync(
            $"/api/cars/{carId}/stock/adjust", new { delta = 3 });
        adjust.EnsureSuccessStatusCode();
        var body = JsonDocument.Parse(await adjust.Content.ReadAsStringAsync());

        Assert.Equal(8, body.RootElement.GetProperty("stock").GetInt32());
    }

    [Fact]
    public async Task AdjustStock_NegativeDeltaTakingBelowZero_Returns400()
    {
        var client = NewClient();
        var token = await RegisterAndLoginAsync(
            client, $"neg-{Guid.NewGuid():N}@example.com", "NegPass1!");
        SetToken(client, token);

        var addResp = await client.PostAsJsonAsync("/api/cars",
            new { make = "Audi", model = "A4", year = 2018, stock = 2 });
        var carId = JsonDocument.Parse(await addResp.Content.ReadAsStringAsync())
            .RootElement.GetProperty("id").GetInt32();

        var adjust = await client.PatchAsJsonAsync(
            $"/api/cars/{carId}/stock/adjust", new { delta = -5 });

        Assert.Equal(HttpStatusCode.BadRequest, adjust.StatusCode);
        var follow = await client.GetFromJsonAsync<JsonElement>($"/api/cars/{carId}");
        Assert.Equal(2, follow.GetProperty("stock").GetInt32());
    }

    [Fact]
    public async Task AdjustStock_ConcurrentDecrements_NeverGoNegative()
    {
        var client = NewClient();
        var token = await RegisterAndLoginAsync(
            client, $"race-{Guid.NewGuid():N}@example.com", "RacePass1!");
        SetToken(client, token);

        var addResp = await client.PostAsJsonAsync("/api/cars",
            new { make = "Audi", model = "A4", year = 2018, stock = 10 });
        var carId = JsonDocument.Parse(await addResp.Content.ReadAsStringAsync())
            .RootElement.GetProperty("id").GetInt32();

        var tasks = Enumerable.Range(0, 20)
            .Select(_ => client.PatchAsJsonAsync(
                $"/api/cars/{carId}/stock/adjust", new { delta = -1 }))
            .ToArray();
        var responses = await Task.WhenAll(tasks);

        var ok = responses.Count(r => r.StatusCode == HttpStatusCode.OK);
        var rejected = responses.Count(r => r.StatusCode == HttpStatusCode.BadRequest);

        Assert.Equal(10, ok);
        Assert.Equal(10, rejected);

        var final = await client.GetFromJsonAsync<JsonElement>($"/api/cars/{carId}");
        Assert.Equal(0, final.GetProperty("stock").GetInt32());
    }

    [Fact]
    public async Task Health_ReturnsHealthy()
    {
        var client = NewClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Healthy", body.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Register_InvalidEmailFormat_Returns400()
    {
        var client = NewClient();

        var response = await client.PostAsJsonAsync("/api/auth/register",
            new { email = "not-an-email", password = "SecurePass1!" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_PasswordTooShort_Returns400()
    {
        var client = NewClient();

        var response = await client.PostAsJsonAsync("/api/auth/register",
            new { email = $"short-{Guid.NewGuid():N}@example.com", password = "Ab1!" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_MissingEmail_Returns400()
    {
        var client = NewClient();

        var response = await client.PostAsJsonAsync("/api/auth/register",
            new { password = "SecurePass1!" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_MissingPassword_Returns400()
    {
        var client = NewClient();

        var response = await client.PostAsJsonAsync("/api/auth/register",
            new { email = $"nopwd-{Guid.NewGuid():N}@example.com" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns409()
    {
        var client = NewClient();
        var email = $"dup-{Guid.NewGuid():N}@example.com";
        await client.PostAsJsonAsync("/api/auth/register", new { email, password = "SecurePass1!" });

        var response = await client.PostAsJsonAsync("/api/auth/register",
            new { email, password = "SecurePass1!" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task AddCar_YearTooOld_Returns400()
    {
        var client = NewClient();
        var token = await RegisterAndLoginAsync(
            client, $"old-{Guid.NewGuid():N}@example.com", "SecurePass1!");
        SetToken(client, token);

        var response = await client.PostAsJsonAsync("/api/cars",
            new { make = "Ford", model = "T", year = 1885, stock = 1 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AddCar_YearTooFuture_Returns400()
    {
        var client = NewClient();
        var token = await RegisterAndLoginAsync(
            client, $"future-{Guid.NewGuid():N}@example.com", "SecurePass1!");
        SetToken(client, token);

        var response = await client.PostAsJsonAsync("/api/cars",
            new { make = "Ford", model = "T", year = DateTime.UtcNow.Year + 2, stock = 1 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AddCar_NegativeStock_Returns400()
    {
        var client = NewClient();
        var token = await RegisterAndLoginAsync(
            client, $"negstock-{Guid.NewGuid():N}@example.com", "SecurePass1!");
        SetToken(client, token);

        var response = await client.PostAsJsonAsync("/api/cars",
            new { make = "Ford", model = "T", year = 2020, stock = -1 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AddCar_MissingMake_Returns400()
    {
        var client = NewClient();
        var token = await RegisterAndLoginAsync(
            client, $"nomake-{Guid.NewGuid():N}@example.com", "SecurePass1!");
        SetToken(client, token);

        var response = await client.PostAsJsonAsync("/api/cars",
            new { model = "T", year = 2020, stock = 1 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
