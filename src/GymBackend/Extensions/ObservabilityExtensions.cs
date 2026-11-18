using System.Net;
using GymBackend.Observability;
using GymBackend.Options;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;

namespace GymBackend.Extensions;

public static class ObservabilityExtensions
{
    public static IServiceCollection AddGymObservability(this IServiceCollection services)
    {
        services.AddOptions<ForwardedHeadersSettings>()
            .BindConfiguration(ForwardedHeadersSettings.SectionName)
            .Validate(o => o.KnownProxies.All(ip => IPAddress.TryParse(ip, out _)),
                "ForwardedHeaders:KnownProxies contiene una IP inválida.")
            .Validate(o => o.KnownNetworks.All(network => System.Net.IPNetwork.TryParse(network, out _)),
                "ForwardedHeaders:KnownNetworks contiene una red CIDR inválida.")
            .ValidateOnStart();
        services.AddOptions<ForwardedHeadersOptions>()
            .Configure<IOptions<ForwardedHeadersSettings>>((options, settings) =>
            {
                if (!settings.Value.Enabled) return;
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                options.ForwardLimit = 1;
                foreach (var proxy in settings.Value.KnownProxies)
                    options.KnownProxies.Add(IPAddress.Parse(proxy));
                foreach (var network in settings.Value.KnownNetworks)
                    options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
            });
        services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database", tags: ["ready"]);
        return services;
    }

    public static WebApplication UseGymObservability(this WebApplication app)
    {
        if (app.Services.GetRequiredService<IOptions<ForwardedHeadersSettings>>().Value.Enabled)
            app.UseForwardedHeaders();
        app.UseMiddleware<CorrelationIdMiddleware>();
        return app;
    }
}
