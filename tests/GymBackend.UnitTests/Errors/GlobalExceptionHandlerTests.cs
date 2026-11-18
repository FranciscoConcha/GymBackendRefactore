using System.Text.Json;
using GymBackend.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace GymBackend.UnitTests.Errors;

public sealed class GlobalExceptionHandlerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnhandledException_WritesSafeProblemDetails(bool uniqueViolation)
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
        Exception exception = uniqueViolation
            ? new Microsoft.EntityFrameworkCore.DbUpdateException(internalMessage,
                new Npgsql.PostgresException(internalMessage, "ERROR", "ERROR", "23505"))
            : new InvalidOperationException(internalMessage);
        var expectedStatus = uniqueViolation ? 409 : 500;
        var handled = await handler.TryHandleAsync(context, exception,
            TestContext.Current.CancellationToken);
        Assert.True(handled);
        Assert.Equal(expectedStatus, context.Response.StatusCode);
        Assert.Equal("application/problem+json", context.Response.ContentType?.Split(';')[0]);
        body.Position = 0;
        using var reader = new StreamReader(body);
        var text = await reader.ReadToEndAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain(internalMessage, text);
        using var json = JsonDocument.Parse(text);
        Assert.Equal(expectedStatus, json.RootElement.GetProperty("status").GetInt32());
        Assert.Equal(uniqueViolation ? "Conflicto con el estado actual del recurso." : "Error interno del servidor.", json.RootElement.GetProperty("title").GetString());
        Assert.False(json.RootElement.TryGetProperty("detail", out _));
    }
}
