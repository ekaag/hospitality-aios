using HospitalityOS.Ports.Execution;

namespace HospitalityOS.Api.Execution;

/// <summary>
/// Starts each request's operation context (P11): joins the caller's flow when it sends a valid
/// <c>X-Correlation-Id</c>, otherwise starts a new one; returns the correlation ID in the response header; and adds it
/// to every log written while handling the request.
/// </summary>
internal sealed class CorrelationMiddleware(RequestDelegate next, ILogger<CorrelationMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context, OperationContext operation)
    {
        if (Guid.TryParse(context.Request.Headers[HeaderName], out Guid callerCorrelation) && callerCorrelation != Guid.Empty)
        {
            operation.JoinFlow(callerCorrelation, causation: null);
        }

        Guid correlationId = operation.CorrelationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId.ToString();

            return Task.CompletedTask;
        });

        using IDisposable? logScope = logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId });

        await next(context);
    }
}
