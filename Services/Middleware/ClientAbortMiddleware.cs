using Microsoft.AspNetCore.Diagnostics;

namespace eInvWorld.Services.Middleware
{
    /// <summary>
    /// A browser that navigates away (or closes the tab) cancels its request, and the query it was waiting for
    /// throws <see cref="OperationCanceledException"/>. That used to be logged as "responded 500" at Error level,
    /// which made real server errors hard to count. Such a request now ends as 499 (client closed request), which
    /// the request log writes at Information level. Nothing is sent: the client has gone.
    /// Only a cancellation of the request itself counts; a server-side timeout or any other exception still fails as before.
    /// </summary>
    public class ClientAbortMiddleware
    {
        private readonly RequestDelegate _next;

        public ClientAbortMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
            {
                if (context.Response.HasStarted) return;
                context.Response.StatusCode = StatusCodes.Status499ClientClosedRequest;
                // No error page for a client that is no longer there.
                var statusPages = context.Features.Get<IStatusCodePagesFeature>();
                if (statusPages != null) statusPages.Enabled = false;
            }
        }
    }
}
