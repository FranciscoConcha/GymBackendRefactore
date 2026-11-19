using GymBackend.Data;
using GymBackend.Time;
using Microsoft.EntityFrameworkCore;

namespace GymBackend.Application.Alerts;

public sealed class NoShowProcessor(AppDbContext db, IGymClock clock)
{
    public async Task<int> RunAsync(CancellationToken ct = default)
    {
        // One statement locks eligible reservations, changes their status and inserts alerts atomically.
        // Compare instants in PostgreSQL, including Santiago's daylight saving transitions.
        var now = clock.UtcNow;
        var zone = clock.TimeZone.Id;
        return await db.Database.ExecuteSqlInterpolatedAsync($"""
            WITH eligible AS (
                SELECT r."Id", r."UserId" FROM "Reservations" r
                JOIN "ScheduleSlots" s ON s."Id" = r."ScheduleSlotId"
                WHERE r."Status" = 'Reservado'
                  AND ((r."Date" + s."EndTime") AT TIME ZONE {zone}) <= {now}
                  AND NOT EXISTS (SELECT 1 FROM "Visits" v WHERE v."ReservationId" = r."Id")
                FOR UPDATE OF r SKIP LOCKED
            ), changed AS (
                UPDATE "Reservations" r SET "Status" = 'Inasistencia'
                FROM eligible e WHERE r."Id" = e."Id" RETURNING r."Id", r."UserId"
            )
            INSERT INTO "AttendanceAlerts" ("UserId", "ReservationId", "CreatedAt")
            SELECT "UserId", "Id", {now} FROM changed
            ON CONFLICT ("ReservationId") DO NOTHING
            """, ct);
    }
}

public sealed class NoShowService(IServiceScopeFactory scopes, IConfiguration configuration, ILogger<NoShowService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var minutes = configuration.GetValue("Alerts:NoShowIntervalMinutes", 15);
        if (minutes <= 0) throw new InvalidOperationException("Alerts:NoShowIntervalMinutes debe ser positivo.");
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(minutes));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<NoShowProcessor>().RunAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { logger.LogError(exception, "Error al procesar inasistencias."); }
        }
    }
}
