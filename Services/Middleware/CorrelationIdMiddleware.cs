using Serilog.Context;

namespace eInvWorld.Services.Middleware
{
    /// <summary>
    /// Assigns a correlation id to every request and makes it available to all logs of that request
    /// (via Serilog's LogContext — already enriched) and to the caller (via the X-Correlation-ID response
    /// header). An incoming X-Correlation-ID is honoured so a correlation can span an upstream caller; if
    /// absent we fall back to the framework's TraceIdentifier. Placed early in the pipeline so every log
    /// line for the request — and the audit entries it writes — carry the same id, making a single
    /// invoice/submission traceable end-to-end.
    /// </summary>
    public class CorrelationIdMiddleware
    {
        private const string HeaderName = "X-Correlation-ID";
        private readonly RequestDelegate _next;

        public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

        public async Task Invoke(HttpContext context)
        {
            var correlationId = context.Request.Headers.TryGetValue(HeaderName, out var incoming)
                                && !string.IsNullOrWhiteSpace(incoming)
                ? incoming.ToString()
                : context.TraceIdentifier;

            // Keep TraceIdentifier in sync so anything reading it (e.g. AuditService) sees the same value.
            context.TraceIdentifier = correlationId;

            // Echo it back before the response body starts.
            context.Response.OnStarting(() =>
            {
                context.Response.Headers[HeaderName] = correlationId;
                return Task.CompletedTask;
            });

            using (LogContext.PushProperty("CorrelationId", correlationId))
            {
                await _next(context);
            }
        }
    }
}
