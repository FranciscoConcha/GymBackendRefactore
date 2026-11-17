using GymBackend.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace GymBackend.Errors;

public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        logger.LogError(exception, "Error no controlado al procesar la solicitud.");
        var isConflict = exception is DbUpdateException updateException && updateException.IsUniqueViolation();
        var status = isConflict ? StatusCodes.Status409Conflict : StatusCodes.Status500InternalServerError;
        httpContext.Response.StatusCode = status;
        await problemDetailsService.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = isConflict ? "Conflicto con el estado actual del recurso." : "Error interno del servidor.",
            },
        });
        return true;
    }
}
