using System.Globalization;
using System.Threading.RateLimiting;
using GymBackend.DTOs;
using GymBackend.Options;
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
                    if (rejected.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                        rejected.HttpContext.Response.Headers["Retry-After"] =
                            Math.Ceiling(retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);

                    await rejected.HttpContext.Response.WriteAsJsonAsync(
                        new MessageResponse(RejectionMessage), cancellationToken);
                };
            });

        services.AddRateLimiter(_ => { });
        return services;
    }
}
