using GymBackend.Data;
using Microsoft.EntityFrameworkCore;

namespace GymBackend.Extensions;

public static class WebApplicationExtensions
{
    public static WebApplication MapHealthEndpoints(this WebApplication app)
    {
        app.MapGet("/health/live", () => Results.Ok(new { status = "ok" })).AllowAnonymous();
        app.MapGet("/health/ready", async (AppDbContext db) =>
        {
            try { return await db.Database.CanConnectAsync() ? Results.Ok(new { status = "ok" }) : Results.StatusCode(503); }
            catch { return Results.StatusCode(503); }
        }).AllowAnonymous();
        return app;
    }
}
