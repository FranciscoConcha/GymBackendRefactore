using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace GymBackend.Extensions;

public static class WebApplicationExtensions
{
    public static WebApplication MapHealthEndpoints(this WebApplication app)
    {
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false,
            ResponseWriter = WriteHealthResponseAsync,
        }).AllowAnonymous();
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains("ready"),
            ResponseWriter = WriteHealthResponseAsync,
        }).AllowAnonymous();
        return app;
    }

    private static Task WriteHealthResponseAsync(HttpContext context, HealthReport report)
    {
        var healthy = report.Status == HealthStatus.Healthy;
        context.Response.StatusCode = healthy ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable;
        return context.Response.WriteAsJsonAsync(new { status = healthy ? "ok" : "unavailable" }, context.RequestAborted);
    }
}
