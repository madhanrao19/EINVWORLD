using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EINVWORLD.Services.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EINVWORLD.Tests
{
    /// <summary>
    /// Unit tests for the provider-agnostic <see cref="AiService"/> orchestrator, using an in-memory fake
    /// provider (no HTTP/Ollama). Proves the master switch, provider selection, default application, and —
    /// most importantly — the "AI never throws into business logic" guarantee that keeps invoicing working
    /// when AI fails.
    /// </summary>
    public class AiServiceTests
    {
        /// <summary>Configurable stand-in for a real transport (Ollama/OpenAI/etc.).</summary>
        private sealed class FakeProvider : IAiProvider
        {
            private readonly Func<AiChatRequest, AiChatResult>? _chat;
            private readonly Func<AiProbeResult>? _probe;
            public FakeProvider(string name, Func<AiChatRequest, AiChatResult>? chat = null, Func<AiProbeResult>? probe = null)
            {
                Name = name; _chat = chat; _probe = probe;
            }
            public string Name { get; }
            public AiChatRequest? LastRequest { get; private set; }
            public int ChatCalls { get; private set; }
            public Task<AiChatResult> ChatAsync(AiChatRequest request, CancellationToken ct = default)
            {
                ChatCalls++;
                LastRequest = request;
                return Task.FromResult(_chat?.Invoke(request) ?? AiChatResult.Success("ok"));
            }
            public Task<AiProbeResult> ProbeAsync(CancellationToken ct = default)
                => Task.FromResult(_probe?.Invoke() ?? new AiProbeResult { Reachable = true, ModelAvailable = true, Detail = "ok" });
        }

        private static AiService Build(AiSettings settings, params IAiProvider[] providers) =>
            new(settings, providers, NullLogger<AiService>.Instance);

        private static AiChatRequest Chat(string text = "hello") =>
            new() { Messages = new List<AiMessage> { new("user", text) } };

        private static AiSettings Enabled(string provider = "Ollama") =>
            new() { Enabled = true, Provider = provider, Temperature = 0.2, MaxTokens = 4096 };

        // ── Master switch ──────────────────────────────────────────────────────────────────────
        [Fact]
        public async Task Disabled_ReturnsGracefulFailure_AndNeverCallsProvider()
        {
            var provider = new FakeProvider("Ollama");
            var svc = Build(new AiSettings { Enabled = false, Provider = "Ollama" }, provider);

            Assert.False(svc.IsEnabled);
            var result = await svc.ChatAsync(Chat());

            Assert.False(result.Ok);
            Assert.Equal(AiErrorKind.Disabled, result.ErrorKind);
            Assert.Equal(0, provider.ChatCalls);
        }

        [Fact]
        public async Task Enabled_ButNoMatchingProvider_IsDisabled()
        {
            // Configured for a provider that isn't registered → treated as disabled, still no throw.
            var svc = Build(Enabled("OpenAI"), new FakeProvider("Ollama"));

            Assert.False(svc.IsEnabled);
            var result = await svc.ChatAsync(Chat());
            Assert.False(result.Ok);
            Assert.Equal(AiErrorKind.Disabled, result.ErrorKind);
        }

        // ── Provider selection ─────────────────────────────────────────────────────────────────
        [Fact]
        public async Task SelectsProviderByName_CaseInsensitive()
        {
            var ollama = new FakeProvider("Ollama", _ => AiChatResult.Success("from-ollama"));
            var openai = new FakeProvider("OpenAI", _ => AiChatResult.Success("from-openai"));
            var svc = Build(Enabled("ollama"), openai, ollama); // configured "ollama" (lowercase)

            var result = await svc.ChatAsync(Chat());

            Assert.True(result.Ok);
            Assert.Equal("from-ollama", result.Content);
            Assert.Equal(1, ollama.ChatCalls);
            Assert.Equal(0, openai.ChatCalls);
        }

        // ── Input validation ───────────────────────────────────────────────────────────────────
        [Fact]
        public async Task EmptyMessages_ReturnsInvalidRequest()
        {
            var svc = Build(Enabled(), new FakeProvider("Ollama"));
            var result = await svc.ChatAsync(new AiChatRequest { Messages = new List<AiMessage>() });
            Assert.Equal(AiErrorKind.InvalidRequest, result.ErrorKind);
        }

        // ── Defaults ───────────────────────────────────────────────────────────────────────────
        [Fact]
        public async Task AppliesConfiguredDefaults_WhenCallerDoesNotOverride()
        {
            var provider = new FakeProvider("Ollama");
            var svc = Build(new AiSettings { Enabled = true, Provider = "Ollama", Temperature = 0.7, MaxTokens = 512 }, provider);

            await svc.ChatAsync(Chat());

            Assert.Equal(0.7, provider.LastRequest!.Temperature);
            Assert.Equal(512, provider.LastRequest!.MaxTokens);
        }

        [Fact]
        public async Task HonoursCallerOverrides()
        {
            var provider = new FakeProvider("Ollama");
            var svc = Build(Enabled(), provider);

            await svc.ChatAsync(new AiChatRequest
            {
                Messages = new List<AiMessage> { new("user", "x") },
                Temperature = 0.0,
                MaxTokens = 42,
            });

            Assert.Equal(0.0, provider.LastRequest!.Temperature);
            Assert.Equal(42, provider.LastRequest!.MaxTokens);
        }

        // ── Result pass-through & the non-throwing guarantee ───────────────────────────────────
        [Fact]
        public async Task PropagatesProviderFailure_WithoutThrowing()
        {
            var svc = Build(Enabled(), new FakeProvider("Ollama",
                _ => AiChatResult.Fail(AiErrorKind.Unreachable, "down")));

            var result = await svc.ChatAsync(Chat());

            Assert.False(result.Ok);
            Assert.Equal(AiErrorKind.Unreachable, result.ErrorKind);
        }

        [Fact]
        public async Task ProviderThatThrows_IsContained_AsUnexpected()
        {
            // A misbehaving provider must never bubble an exception into invoice flows.
            var svc = Build(Enabled(), new FakeProvider("Ollama",
                _ => throw new InvalidOperationException("boom")));

            var result = await svc.ChatAsync(Chat());

            Assert.False(result.Ok);
            Assert.Equal(AiErrorKind.Unexpected, result.ErrorKind);
        }

        // ── Test connection ────────────────────────────────────────────────────────────────────
        [Fact]
        public async Task TestConnection_WhenDisabled_ReportsUnreachable()
        {
            var svc = Build(new AiSettings { Enabled = false, Provider = "Ollama" }, new FakeProvider("Ollama"));
            var probe = await svc.TestConnectionAsync();
            Assert.False(probe.Reachable);
        }

        [Fact]
        public async Task TestConnection_DelegatesToProviderProbe()
        {
            var svc = Build(Enabled(), new FakeProvider("Ollama",
                probe: () => new AiProbeResult { Reachable = true, ModelAvailable = true, Detail = "great", LatencyMs = 5 }));

            var probe = await svc.TestConnectionAsync();

            Assert.True(probe.Reachable);
            Assert.True(probe.ModelAvailable);
            Assert.Equal("great", probe.Detail);
        }

        [Fact]
        public async Task ProviderProbeThatThrows_IsContained()
        {
            var svc = Build(Enabled(), new FakeProvider("Ollama",
                probe: () => throw new InvalidOperationException("boom")));

            var probe = await svc.TestConnectionAsync();
            Assert.False(probe.Reachable);
        }
    }
}
