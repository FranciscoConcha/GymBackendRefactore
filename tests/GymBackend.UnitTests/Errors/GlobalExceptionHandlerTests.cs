using System.Text.Json;
using GymBackend.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace GymBackend.UnitTests.Errors;

public sealed class GlobalExceptionHandlerTests
{
    [Fact]
    public async Task UnhandledException_WritesSafeProblemDetails()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddProblemDetails();
        using var provider = services.BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = provider };
        await using var body = new MemoryStream();
        context.Response.Body = body;
        var handler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance,
            provider.GetRequiredService<IProblemDetailsService>());
        const string internalMessage = "Internal database credentials must never appear";
        var handled = await handler.TryHandleAsync(context, new InvalidOperationException(internalMessage),
            TestContext.Current.CancellationToken);
        Assert.True(handled);
        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.Equal("application/problem+json", context.Response.ContentType?.Split(';')[0]);
        body.Position = 0;
        using var reader = new StreamReader(body);
        var text = await reader.ReadToEndAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain(internalMessage, text);
        using var json = JsonDocument.Parse(text);
        Assert.Equal(500, json.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("Error interno del servidor.", json.RootElement.GetProperty("title").GetString());
        Assert.False(json.RootElement.TryGetProperty("detail", out _));
    }
}
