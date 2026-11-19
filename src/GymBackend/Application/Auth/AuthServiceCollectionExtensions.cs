using GymBackend.Application.Auth;

namespace GymBackend.Extensions;

public static class AuthServiceCollectionExtensions
{
    public static IServiceCollection AddAuthServices(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddHttpClient("brevo", (provider, client) =>
        {
            var settings = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<GymBackend.Options.EmailOptions>>().Value;
            client.BaseAddress = new Uri(settings.ApiBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
        }).RemoveAllLoggers()
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        services.AddSingleton<GymBackend.Application.Email.BrevoApiEmailSender>();
        services.AddScoped<GymBackend.Application.Audit.IAuditLog, GymBackend.Application.Audit.AuditLog>();
        services.AddScoped<GymBackend.Application.Branches.BranchService>();
        services.AddScoped<AccountService>();
        services.AddHostedService<GymBackend.Application.Email.EmailOutboxDispatcher>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<IUserPasswordHasher, Pbkdf2PasswordHasher>();
        return services;
    }
}
