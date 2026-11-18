namespace GymBackend.Security;

public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers.TryAdd("X-Content-Type-Options", "nosniff");
            headers.TryAdd("X-Frame-Options", "DENY");
            headers.TryAdd("Referrer-Policy", "no-referrer");
            if (!(context.Request.Path.Value?.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase) ?? false))
                headers.TryAdd("Content-Security-Policy", "default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'");
            headers.TryAdd("Cross-Origin-Opener-Policy", "same-origin");
            headers.TryAdd("Permissions-Policy", "camera=(), microphone=(), geolocation=(), payment=()");
            if (context.Request.Path.Value?.StartsWith("/api/", StringComparison.OrdinalIgnoreCase) == true)
                headers.TryAdd("Cache-Control", "no-store");
            return Task.CompletedTask;
        });
        return next(context);
    }
}
