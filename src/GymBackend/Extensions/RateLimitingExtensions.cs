using System.Globalization;
using System.Threading.RateLimiting;
using GymBackend.Data;
using GymBackend.DTOs;
using GymBackend.Models;
using GymBackend.Options;
using GymBackend.Time;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace GymBackend.Extensions;

public static class RateLimitingExtensions
{
    public const string LoginPolicy = "login";
    private const string RejectionMessage = "Demasiados intentos de inicio de sesión. Intente nuevamente más tarde.";

    public static IServiceCollection AddGymRateLimiting(this IServiceCollection services)
    {
        services.AddOptions<RateLimitingOptions>()
            .BindConfiguration(RateLimitingOptions.SectionName)
            .Validate(o => o.Login.PermitLimit > 0 && o.Login.WindowSeconds > 0,
                "RateLimiting:Login:PermitLimit y WindowSeconds deben ser mayores que cero.")
            .ValidateOnStart();

        services.AddOptions<RateLimiterOptions>()
            .Configure<IOptions<RateLimitingOptions>>((rateLimiter, settings) =>
            {
                var login = settings.Value.Login;
                rateLimiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                rateLimiter.AddPolicy<string>("access", context =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        context.User.GetUserId()?.ToString(CultureInfo.InvariantCulture) ?? "anonymous",
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 60,
                            Window = TimeSpan.FromMinutes(1),
                            QueueLimit = 0,
                            AutoReplenishment = true,
                        }));
                rateLimiter.AddPolicy<string>(LoginPolicy, context =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = login.PermitLimit,
                            Window = TimeSpan.FromSeconds(login.WindowSeconds),
                            QueueLimit = 0,
                            AutoReplenishment = true,
                        }));
                rateLimiter.OnRejected = async (rejected, cancellationToken) =>
                {
                    var http = rejected.HttpContext;
                    var access = http.Request.Path.StartsWithSegments("/api/v1/access");
                    var message = access ? "Demasiados intentos de acceso. Intente nuevamente más tarde." : RejectionMessage;
                    if (access && HttpMethods.IsPost(http.Request.Method) && http.User.GetUserId() is { } operatorId)
                    {
                        var action = http.Request.Path.Value?.Split('/').Last() switch
                        {
                            "validate" => "Validar",
                            "entry" => "Entrada",
                            "exit" => "Salida",
                            _ => null,
                        };
                        if (action is not null)
                        {
                            var db = http.RequestServices.GetRequiredService<AppDbContext>();
                            db.AccessEvents.Add(new AccessEvent
                            {
                                OperatorId = operatorId,
                                Action = action,
                                Result = "Rechazado",
                                Reason = message,
                                OccurredAt = http.RequestServices.GetRequiredService<IGymClock>().UtcNow
                            });
                            await db.SaveChangesAsync(cancellationToken);
                        }
                    }
                    if (rejected.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                        rejected.HttpContext.Response.Headers["Retry-After"] =
                            Math.Ceiling(retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);

                    await rejected.HttpContext.Response.WriteAsJsonAsync(
                        new MessageResponse(message), cancellationToken);
                };
            });

        services.AddRateLimiter(_ => { });
        return services;
    }
}
