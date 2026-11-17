using GymBackend.Data;
using GymBackend.Time;
using Microsoft.Extensions.DependencyInjection.Extensions;
using GymBackend.Options;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace GymBackend.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddGymErrors(this IServiceCollection services)
    {
        services.AddProblemDetails();
        services.AddExceptionHandler<GymBackend.Errors.GlobalExceptionHandler>();
        return services;
    }

    public static IServiceCollection AddGymOptions(this IServiceCollection services)
    {
        services.AddOptions<GymOptions>()
            .BindConfiguration(GymOptions.SectionName)
            .Validate(o => TimeZoneInfo.TryFindSystemTimeZoneById(o.TimeZone, out _),
                "Configure Gym:TimeZone con un identificador IANA válido (p. ej. America/Santiago).")
            .ValidateOnStart();
        services.AddOptions<JwtOptions>()
            .BindConfiguration(JwtOptions.SectionName)
            .Validate(o => !string.IsNullOrWhiteSpace(o.Key) && o.Key.Length >= 32,
                "Configure Jwt:Key con al menos 32 caracteres.")
            .ValidateOnStart();
        services.AddOptions<CorsSettings>().BindConfiguration(CorsSettings.SectionName).ValidateOnStart();
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
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(1),
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
                    if (origins.Length > 0) policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
                }));
        return services;
    }
}
