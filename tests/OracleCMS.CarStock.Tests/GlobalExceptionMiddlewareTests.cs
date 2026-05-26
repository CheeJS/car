using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using OracleCMS.CarStock.API.Middleware;

namespace OracleCMS.CarStock.Tests;

public sealed class GlobalExceptionMiddlewareTests
{
    [Fact]
    public async Task Invoke_WhenNextThrows_Returns500WithErrorEnvelope()
    {
        var middleware = new GlobalExceptionMiddleware(
            _ => throw new InvalidOperationException("boom"),
            NullLogger<GlobalExceptionMiddleware>.Instance);

        var context = new DefaultHttpContext();
        var responseBody = new MemoryStream();
        context.Response.Body = responseBody;
        context.Request.Method = "GET";
        context.Request.Path = "/api/cars";

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.StartsWith("application/json", context.Response.ContentType);

        responseBody.Position = 0;
        using var json = await JsonDocument.ParseAsync(responseBody);
        var root = json.RootElement;

        Assert.Equal("An unexpected error occurred.", root.GetProperty("error").GetString());
        var correlationId = root.GetProperty("correlationId").GetString();
        Assert.False(string.IsNullOrWhiteSpace(correlationId));
        Assert.Equal(8, correlationId!.Length);
        Assert.DoesNotContain("boom", JsonSerializer.Serialize(root));
        Assert.DoesNotContain("InvalidOperationException", JsonSerializer.Serialize(root));
    }

    [Fact]
    public async Task Invoke_WhenNextSucceeds_DoesNotMutateResponse()
    {
        var middleware = new GlobalExceptionMiddleware(
            ctx =>
            {
                ctx.Response.StatusCode = StatusCodes.Status200OK;
                return Task.CompletedTask;
            },
            NullLogger<GlobalExceptionMiddleware>.Instance);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }
}
