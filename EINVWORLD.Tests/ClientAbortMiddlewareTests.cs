using System;
using System.Threading;
using System.Threading.Tasks;
using eInvWorld.Services.Middleware;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace EINVWORLD.Tests
{
    /// <summary>A request the browser cancelled ends as 499, not as a logged 500; real failures still throw.</summary>
    public class ClientAbortMiddlewareTests
    {
        private static DefaultHttpContext Context(bool aborted)
        {
            var context = new DefaultHttpContext { RequestAborted = new CancellationToken(aborted) };
            context.Features.Set<IStatusCodePagesFeature>(new StatusCodePagesFeature());
            return context;
        }

        [Theory]
        [InlineData(typeof(OperationCanceledException))]
        [InlineData(typeof(TaskCanceledException))]   // what EF Core throws when the connection open is cancelled
        public async Task ClientCancelledRequest_Ends499_WithoutAnErrorPage(Type exception)
        {
            var context = Context(aborted: true);
            await new ClientAbortMiddleware(_ => throw (Exception)Activator.CreateInstance(exception)!).Invoke(context);

            Assert.Equal(499, context.Response.StatusCode);
            Assert.False(context.Features.Get<IStatusCodePagesFeature>()!.Enabled);
        }

        [Fact]
        public async Task ServerSideCancellation_StillFails()
        {
            // e.g. an outgoing HTTP call timed out: the client is still waiting, so this is a real error.
            var context = Context(aborted: false);
            await Assert.ThrowsAsync<TaskCanceledException>(() => new ClientAbortMiddleware(_ => throw new TaskCanceledException()).Invoke(context));
        }

        [Fact]
        public async Task OtherException_OnACancelledRequest_StillFails()
        {
            var context = Context(aborted: true);
            await Assert.ThrowsAsync<InvalidOperationException>(() => new ClientAbortMiddleware(_ => throw new InvalidOperationException()).Invoke(context));
        }

        [Fact]
        public async Task NormalRequest_IsUntouched()
        {
            var context = Context(aborted: false);
            await new ClientAbortMiddleware(c => { c.Response.StatusCode = 204; return Task.CompletedTask; }).Invoke(context);

            Assert.Equal(204, context.Response.StatusCode);
            Assert.True(context.Features.Get<IStatusCodePagesFeature>()!.Enabled);
        }
    }
}
