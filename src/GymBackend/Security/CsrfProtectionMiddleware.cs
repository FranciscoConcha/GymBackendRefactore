using GymBackend.DTOs;
using GymBackend.Options;
using Microsoft.Extensions.Options;

namespace GymBackend.Security;

public sealed class CsrfProtectionMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Gym-Csrf";
    public const string HeaderValue = "1";

    public async Task InvokeAsync(HttpContext context, IOptions<CorsSettings> settings)
    {
        var request = context.Request;
        if (request.Path.StartsWithSegments("/api") &&
            (HttpMethods.IsPost(request.Method) || HttpMethods.IsPut(request.Method) ||
             HttpMethods.IsPatch(request.Method) || HttpMethods.IsDelete(request.Method)) &&
            !request.Headers.ContainsKey("Authorization"))
        {
            var origin = request.Headers.Origin;
            if (request.Headers[HeaderName] != HeaderValue ||
                (request.Headers.ContainsKey("Origin") &&
                 (origin.Count != 1 || !settings.Value.AllowedOrigins.Any(allowed =>
                     string.Equals(allowed.TrimEnd('/'), origin[0], StringComparison.OrdinalIgnoreCase)))))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new MessageResponse("Solicitud rechazada por protección CSRF."), context.RequestAborted);
                return;
            }
        }
        await next(context);
    }
}
