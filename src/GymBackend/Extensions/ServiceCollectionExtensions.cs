using System.Security.Claims;
using System.Text;
using GymBackend.Data;
using GymBackend.Options;
using GymBackend.Security;
using GymBackend.Time;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace GymBackend.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddGymErrors(this IServiceCollection services)
    {
        services.AddProblemDetails(o => o.CustomizeProblemDetails = ctx =>
            ctx.ProblemDetails.Extensions["correlationId"] = ctx.HttpContext.TraceIdentifier);
        services.AddExceptionHandler<GymBackend.Errors.GlobalExceptionHandler>();
        return services;
    }

    public static IServiceCollection AddGymOptions(this IServiceCollection services)
    {
        services.AddOptions<ReservationOptions>()
            .BindConfiguration(ReservationOptions.SectionName)
            .Validate(o => o.MaxDaysAhead is >= 0 and <= 60, "Reservations:MaxDaysAhead debe estar entre 0 y 60.")
            .Validate(o => Enum.IsDefined(o.HorizonUnit), "Reservations:HorizonUnit inválido.")
            .ValidateOnStart();
        services.AddOptions<GymOptions>()
            .BindConfiguration(GymOptions.SectionName)
            .Validate(o => TimeZoneInfo.TryFindSystemTimeZoneById(o.TimeZone, out _),
                "Configure Gym:TimeZone con un identificador IANA válido (p. ej. America/Santiago).")
            .ValidateOnStart();
        services.AddOptions<JwtOptions>()
            .BindConfiguration(JwtOptions.SectionName)
            .Validate(o => !string.IsNullOrWhiteSpace(o.Key) && o.Key.Length >= 32,
                "Configure Jwt:Key con al menos 32 caracteres.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.Issuer), "Configure Jwt:Issuer.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.Audience), "Configure Jwt:Audience.")
            .ValidateOnStart();
        services.AddOptions<QrOptions>()
            .BindConfiguration(QrOptions.SectionName)
            .Validate(o => !string.IsNullOrWhiteSpace(o.SigningKey) && o.SigningKey.Length >= 32,
                "Configure Qr:SigningKey con al menos 32 caracteres.")
            .Validate<IOptions<JwtOptions>>((o, jwt) => !string.Equals(o.SigningKey, jwt.Value.Key, StringComparison.Ordinal),
                "Qr:SigningKey debe ser distinta de Jwt:Key.")
            .ValidateOnStart();
        services.AddOptions<CorsSettings>().BindConfiguration(CorsSettings.SectionName)
            .Validate(o => !o.AllowedOrigins.Contains("*"), "Cors:AllowedOrigins no permite *.")
            .ValidateOnStart();
        services.AddOptions<DatabaseOptions>().BindConfiguration(DatabaseOptions.SectionName).ValidateOnStart();
        services.AddOptions<BootstrapOptions>().BindConfiguration(BootstrapOptions.SectionName).ValidateOnStart();
        return services;
    }

    public static IServiceCollection AddGymTime(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IGymClock, GymClock>();
        return services;
    }

    public static IServiceCollection AddGymAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwt) =>
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Value.Key)),
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Value.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Value.Audience,
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(1),
                });
        services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, bearer =>
        {
            bearer.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    if (!context.Request.Headers.ContainsKey("Authorization"))
                        context.Token = context.Request.Cookies[SessionCookie.Name];
                    return Task.CompletedTask;
                },
                OnTokenValidated = async context =>
                {
                    if (!int.TryParse(context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId))
                        return;

                    var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
                    var enabled = await db.Users.AsNoTracking()
                        .Where(user => user.Id == userId)
                        .Select(user => (bool?)user.Enabled)
                        .SingleOrDefaultAsync(context.HttpContext.RequestAborted);
                    if (enabled == false)
                        context.Fail("El usuario no está habilitado.");
                },
            };
        });
        services.AddAuthorization();
        return services;
    }

    public static IServiceCollection AddGymPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Configure ConnectionStrings:DefaultConnection.");

        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
        return services;
    }

    public static IServiceCollection AddGymCors(this IServiceCollection services)
    {
        services.AddCors();
        services.AddOptions<Microsoft.AspNetCore.Cors.Infrastructure.CorsOptions>()
            .Configure<IOptions<CorsSettings>>((cors, settings) =>
                cors.AddPolicy("Frontend", policy =>
                {
                    var origins = settings.Value.AllowedOrigins;
                    if (origins.Length > 0) policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
                }));
        return services;
    }
}

