namespace GymBackend.Observability;

public sealed class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var values = context.Request.Headers["X-Correlation-ID"];
        var candidate = values.Count == 1 ? values[0] : null;
        var id = candidate is { Length: >= 1 and <= 64 }
            && candidate.All(c => c is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '.' or '_' or '-')
                ? candidate : context.TraceIdentifier;
        context.TraceIdentifier = id;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers["X-Correlation-ID"] = id;
            return Task.CompletedTask;
        });
        using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = id }))
        {
            await next(context);
        }
    }
}
