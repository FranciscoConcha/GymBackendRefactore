using GymBackend.Data;
using GymBackend.Extensions;
using GymBackend.Observability;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace GymBackend.UnitTests.Observability;

public sealed class ObservabilityTests
{
    [Theory]
    [InlineData(500)]
    [InlineData(409)]
    public void ProblemDetails_UsesTraceIdentifier(int status)
    {
        var services = new ServiceCollection();
        services.AddGymErrors();
        using var provider = services.BuildServiceProvider();
        var context = new ProblemDetailsContext
        {
            HttpContext = new DefaultHttpContext { TraceIdentifier = "request-123" },
            ProblemDetails = new ProblemDetails { Status = status },
        };
        provider.GetRequiredService<IOptions<ProblemDetailsOptions>>().Value.CustomizeProblemDetails!(context);
        Assert.Equal("request-123", context.ProblemDetails.Extensions["correlationId"]);
    }

    [Fact]
    public async Task DatabaseHealthCheck_UnreachableDatabase_IsUnhealthy()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=health;Username=health;Password=health;Timeout=1;Pooling=false")
            .Options;
        await using var db = new AppDbContext(options);
        var result = await new DatabaseHealthCheck(db).CheckHealthAsync(new HealthCheckContext(),
            TestContext.Current.CancellationToken);
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }
}
