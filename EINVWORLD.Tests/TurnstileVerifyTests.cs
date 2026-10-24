using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using eInvWorld.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EINVWORLD.Tests
{
    /// <summary>Turnstile (CAPTCHA) check fails closed and never turns a Cloudflare outage into a 500 on sign-in.</summary>
    public class TurnstileVerifyTests
    {
        private sealed class Stub : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _reply;
            public Uri? LastUri;
            public Stub(Func<HttpRequestMessage, HttpResponseMessage> reply) => _reply = reply;
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            {
                LastUri = request.RequestUri;
                return Task.FromResult(_reply(request));
            }
        }

        private static HttpResponseMessage Json(string body, HttpStatusCode code = HttpStatusCode.OK) =>
            new(code) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };

        private static Task<bool> Verify(Stub stub, string? token = "tok", string? url = null) =>
            SecureFormPageModel.VerifyTurnstileAsync(new HttpClient(stub), url, "secret", token, NullLogger.Instance);

        [Fact]
        public async Task Accepts_Only_A_Successful_Verification()
        {
            Assert.True(await Verify(new Stub(_ => Json("{\"success\":true}"))));
            Assert.False(await Verify(new Stub(_ => Json("{\"success\":false}"))));
        }

        [Fact]
        public async Task Missing_Token_Is_Refused_Without_Calling_Cloudflare()
        {
            var stub = new Stub(_ => throw new InvalidOperationException("must not be called"));
            Assert.False(await Verify(stub, token: " "));
            Assert.Null(stub.LastUri);
        }

        [Fact]
        public async Task Outage_Bad_Status_Or_Garbage_Is_Refused_Not_Thrown()
        {
            Assert.False(await Verify(new Stub(_ => throw new HttpRequestException("network down"))));
            Assert.False(await Verify(new Stub(_ => Json("{}", HttpStatusCode.ServiceUnavailable))));
            Assert.False(await Verify(new Stub(_ => Json("<html>not json</html>"))));
        }

        [Fact]
        public async Task Uses_The_Configured_Url_Or_Cloudflare_By_Default()
        {
            var stub = new Stub(_ => Json("{\"success\":true}"));
            await Verify(stub, url: "https://verify.example.test/siteverify");
            Assert.Equal("https://verify.example.test/siteverify", stub.LastUri!.ToString());
            await Verify(stub, url: null);
            Assert.Equal(SecureFormPageModel.DefaultTurnstileVerifyUrl, stub.LastUri!.ToString());
        }
    }
}
