using GymBackend.Application.Auth;

namespace GymBackend.Extensions;

public static class AuthServiceCollectionExtensions
{
    public static IServiceCollection AddAuthServices(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<IUserPasswordHasher, LegacySha256PasswordHasher>();
        return services;
    }
}
