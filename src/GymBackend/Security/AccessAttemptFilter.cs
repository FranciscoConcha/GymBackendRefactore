using GymBackend.Data;
using GymBackend.DTOs;
using GymBackend.Extensions;
using GymBackend.Models;
using GymBackend.Time;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace GymBackend.Security;

// Capture requests rejected by binding/idempotency before the business service runs.
public sealed class AccessAttemptFilter(AppDbContext db, IGymClock clock) : IAsyncResourceFilter, IOrderedFilter
{
    public int Order => -3000;

    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var executed = await next();
        var http = context.HttpContext;
        if (!HttpMethods.IsPost(http.Request.Method) || http.User.GetUserId() is not { } operatorId
            || db.ChangeTracker.Entries<AccessEvent>().Any() || (executed.Exception is not null && !executed.ExceptionHandled)) return;
        var action = http.Request.Path.Value?.Split('/').Last() switch
        {
            "validate" => "Validar",
            "entry" => "Entrada",
            "exit" => "Salida",
            _ => null,
        };
        if (action is null) return;
        var rejected = http.Response.StatusCode >= 400;
        db.AccessEvents.Add(new AccessEvent
        {
            OperatorId = operatorId,
            OccurredAt = clock.UtcNow,
            Action = action,
            Result = rejected ? "Rechazado" : "Aceptado",
            Reason = rejected ? (executed.Result as ObjectResult)?.Value is MessageResponse message
                ? message.Message : "Solicitud de acceso inválida." : null,
        });
        await db.SaveChangesAsync(http.RequestAborted);
    }
}
