using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using EINVWORLD.Services.Background;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EINVWORLD.Tests.Services
{
    /// <summary>
    /// Exercises TessdataSyncWorker.SyncAsync's file-level policy against a stubbed HTTP handler and a
    /// real temp directory (no database, no real network) — missing files are downloaded, unchanged
    /// files are skipped, implausibly small/corrupt responses are discarded, and multiple configured
    /// languages (OcrLanguage split on '+') are all fetched.
    /// </summary>
    public class TessdataSyncWorkerTests : IDisposable
    {
        private readonly string _tempDir;

        public TessdataSyncWorkerTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "einvworld-tessdata-tests-" + Guid.NewGuid());
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true);
        }

        private sealed class StubHandler : HttpMessageHandler
        {
            public int GetCalls;
            public int HeadCalls;
            public byte[] Body = new byte[200_000]; // above the 100KB plausibility floor

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            {
                if (request.Method == HttpMethod.Head)
                {
                    Interlocked.Increment(ref HeadCalls);
                    var resp = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Array.Empty<byte>()) };
                    resp.Content.Headers.ContentLength = Body.Length;
                    return Task.FromResult(resp);
                }

                Interlocked.Increment(ref GetCalls);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Body) });
            }
        }

        private sealed class StubHttpClientFactory : IHttpClientFactory
        {
            public HttpMessageHandler Handler = new StubHandler();
            public HttpClient CreateClient(string name) => new HttpClient(Handler, disposeHandler: false);
        }

        private static IConfiguration BuildConfig(string ocrLanguage = "eng") =>
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TessdataSync:BaseUrl"] = "https://stub.invalid/tessdata/",
                ["DocumentCapture:OcrLanguage"] = ocrLanguage,
            }).Build();

        [Fact]
        public async Task SyncAsync_DownloadsMissingFile()
        {
            var factory = new StubHttpClientFactory();
            var worker = new TessdataSyncWorker(factory, BuildConfig(), NullLogger<TessdataSyncWorker>.Instance);

            await worker.SyncAsync(_tempDir, CancellationToken.None);

            var target = Path.Combine(_tempDir, "eng.traineddata");
            Assert.True(File.Exists(target));
            Assert.False(File.Exists(target + ".downloading"));
            Assert.Equal(200_000, new FileInfo(target).Length);
        }

        [Fact]
        public async Task SyncAsync_SkipsUnchangedFile()
        {
            var factory = new StubHttpClientFactory();
            var target = Path.Combine(_tempDir, "eng.traineddata");
            await File.WriteAllBytesAsync(target, new byte[200_000]); // matches stub's Content-Length

            var worker = new TessdataSyncWorker(factory, BuildConfig(), NullLogger<TessdataSyncWorker>.Instance);
            await worker.SyncAsync(_tempDir, CancellationToken.None);

            Assert.Equal(0, ((StubHandler)factory.Handler).GetCalls); // never re-downloaded
        }

        [Fact]
        public async Task SyncAsync_DiscardsImplausiblySmallDownload()
        {
            var factory = new StubHttpClientFactory();
            ((StubHandler)factory.Handler).Body = new byte[1024]; // far below the 100KB plausibility floor
            var worker = new TessdataSyncWorker(factory, BuildConfig(), NullLogger<TessdataSyncWorker>.Instance);

            await worker.SyncAsync(_tempDir, CancellationToken.None);

            var target = Path.Combine(_tempDir, "eng.traineddata");
            Assert.False(File.Exists(target));
            Assert.False(File.Exists(target + ".downloading")); // temp file cleaned up, never left behind
        }

        [Fact]
        public async Task SyncAsync_FetchesEveryConfiguredLanguage()
        {
            var factory = new StubHttpClientFactory();
            var worker = new TessdataSyncWorker(factory, BuildConfig("eng+msa"), NullLogger<TessdataSyncWorker>.Instance);

            await worker.SyncAsync(_tempDir, CancellationToken.None);

            Assert.True(File.Exists(Path.Combine(_tempDir, "eng.traineddata")));
            Assert.True(File.Exists(Path.Combine(_tempDir, "msa.traineddata")));
        }

        private sealed class OneLanguageFailsHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            {
                if (request.RequestUri!.ToString().Contains("bad.traineddata"))
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));

                if (request.Method == HttpMethod.Head)
                {
                    var resp = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Array.Empty<byte>()) };
                    resp.Content.Headers.ContentLength = 200_000;
                    return Task.FromResult(resp);
                }
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[200_000]) });
            }
        }

        [Fact]
        public async Task SyncAsync_OneLanguageFailureDoesNotBlockOthers()
        {
            var factory = new StubHttpClientFactory { Handler = new OneLanguageFailsHandler() };
            var worker = new TessdataSyncWorker(factory, BuildConfig("bad+eng"), NullLogger<TessdataSyncWorker>.Instance);

            await worker.SyncAsync(_tempDir, CancellationToken.None);

            Assert.False(File.Exists(Path.Combine(_tempDir, "bad.traineddata")));
            Assert.True(File.Exists(Path.Combine(_tempDir, "eng.traineddata")));
        }
    }
}
