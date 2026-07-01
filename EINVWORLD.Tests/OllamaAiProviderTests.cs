using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EINVWORLD.Services.AI;
using EINVWORLD.Services.AI.Providers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EINVWORLD.Tests
{
    /// <summary>
    /// Exercises the Ollama provider's HTTP-to-typed-result mapping with a stub <see cref="HttpMessageHandler"/>
    /// (no real Ollama). Covers the spec's transport cases: successful chat, service unavailable, invalid/
    /// unpulled model, timeout, empty response, and the connectivity probe (model present / not pulled).
    /// </summary>
    public class OllamaAiProviderTests
    {
        /// <summary>Returns a canned response — or throws — for each request; a thrown exception faults the task,
        /// mirroring how <see cref="HttpClient"/> surfaces connection/timeout failures.</summary>
        private sealed class StubHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
            public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            {
                try { return Task.FromResult(_responder(request)); }
                catch (Exception ex) { return Task.FromException<HttpResponseMessage>(ex); }
            }
        }

        private static OllamaAiProvider Provider(HttpMessageHandler handler, AiSettings? settings = null)
        {
            settings ??= new AiSettings { BaseUrl = "http://localhost:11434", Model = "gemma3:12b" };
            var http = new HttpClient(handler) { BaseAddress = new Uri(settings.BaseUrl) };
            return new OllamaAiProvider(http, settings, NullLogger<OllamaAiProvider>.Instance);
        }

        private static HttpResponseMessage Json(HttpStatusCode code, string body) =>
            new(code) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

        private static AiChatRequest Chat() =>
            new() { Messages = new List<AiMessage> { new("user", "hi") } };

        // ── ChatAsync ──────────────────────────────────────────────────────────────────────────
        [Fact]
        public async Task Chat_Success_ReturnsContent()
        {
            var p = Provider(new StubHandler(_ =>
                Json(HttpStatusCode.OK, "{\"message\":{\"role\":\"assistant\",\"content\":\"Hello!\"}}")));

            var r = await p.ChatAsync(Chat());

            Assert.True(r.Ok);
            Assert.Equal("Hello!", r.Content);
        }

        [Fact]
        public async Task Chat_ConnectionRefused_MapsToUnreachable()
        {
            var p = Provider(new StubHandler(_ => throw new HttpRequestException("refused")));

            var r = await p.ChatAsync(Chat());

            Assert.False(r.Ok);
            Assert.Equal(AiErrorKind.Unreachable, r.ErrorKind);
        }

        [Fact]
        public async Task Chat_ModelNotFound_MapsToModelUnavailable()
        {
            var p = Provider(new StubHandler(_ =>
                Json(HttpStatusCode.NotFound, "{\"error\":\"model 'gemma3:12b' not found, try pulling it\"}")));

            var r = await p.ChatAsync(Chat());

            Assert.False(r.Ok);
            Assert.Equal(AiErrorKind.ModelUnavailable, r.ErrorKind);
            Assert.Contains("ollama pull", r.Error);
        }

        [Fact]
        public async Task Chat_Timeout_MapsToTimeout()
        {
            // HttpClient surfaces a request timeout as TaskCanceledException while the caller's token is not cancelled.
            var p = Provider(new StubHandler(_ => throw new TaskCanceledException("timeout")));

            var r = await p.ChatAsync(Chat(), CancellationToken.None);

            Assert.False(r.Ok);
            Assert.Equal(AiErrorKind.Timeout, r.ErrorKind);
        }

        [Fact]
        public async Task Chat_EmptyContent_MapsToEmptyResponse()
        {
            var p = Provider(new StubHandler(_ =>
                Json(HttpStatusCode.OK, "{\"message\":{\"role\":\"assistant\",\"content\":\"\"}}")));

            var r = await p.ChatAsync(Chat());

            Assert.False(r.Ok);
            Assert.Equal(AiErrorKind.EmptyResponse, r.ErrorKind);
        }

        [Fact]
        public async Task Chat_ServerError_MapsToProviderError()
        {
            var p = Provider(new StubHandler(_ => Json(HttpStatusCode.InternalServerError, "boom")));

            var r = await p.ChatAsync(Chat());

            Assert.False(r.Ok);
            Assert.Equal(AiErrorKind.ProviderError, r.ErrorKind);
        }

        // ── ProbeAsync ─────────────────────────────────────────────────────────────────────────
        [Fact]
        public async Task Probe_ModelPresent_ReportsReachableAndAvailable()
        {
            var p = Provider(new StubHandler(_ =>
                Json(HttpStatusCode.OK, "{\"models\":[{\"name\":\"gemma3:12b\"},{\"name\":\"qwen3:32b\"}]}")));

            var probe = await p.ProbeAsync();

            Assert.True(probe.Reachable);
            Assert.True(probe.ModelAvailable);
            Assert.Contains("gemma3:12b", probe.AvailableModels);
        }

        [Fact]
        public async Task Probe_ModelMissing_ReachableButNotAvailable()
        {
            var p = Provider(new StubHandler(_ =>
                Json(HttpStatusCode.OK, "{\"models\":[{\"name\":\"llama3.2:3b\"}]}")));

            var probe = await p.ProbeAsync();

            Assert.True(probe.Reachable);
            Assert.False(probe.ModelAvailable);
        }

        [Fact]
        public async Task Probe_Unreachable_ReportsUnreachable()
        {
            var p = Provider(new StubHandler(_ => throw new HttpRequestException("refused")));

            var probe = await p.ProbeAsync();

            Assert.False(probe.Reachable);
        }
    }
}
