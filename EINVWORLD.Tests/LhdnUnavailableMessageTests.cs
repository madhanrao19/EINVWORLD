using System.Net;
using Xunit;

namespace EINVWORLD.Tests
{
    /// <summary>
    /// A 5xx from LHDN's gateway (an HTML error page) must surface as "LHDN unavailable" instead of a
    /// JSON parse error; 4xx/429 keep their existing handling. Message must not contain "404"/"429",
    /// which InvoiceSubmissionHelper matches on to classify retries.
    /// </summary>
    public class LhdnUnavailableMessageTests
    {
        [Theory]
        [InlineData(HttpStatusCode.InternalServerError)]
        [InlineData(HttpStatusCode.BadGateway)]
        [InlineData(HttpStatusCode.ServiceUnavailable)]
        [InlineData(HttpStatusCode.GatewayTimeout)]
        public void ServerErrors_AreReportedAsUnavailable(HttpStatusCode status)
        {
            var msg = LHDNApiService.LhdnUnavailableMessage(status);
            Assert.NotNull(msg);
            Assert.StartsWith("LHDN unavailable", msg);
            Assert.Contains(((int)status).ToString(), msg);
            Assert.DoesNotContain("404", msg);
            Assert.DoesNotContain("429", msg);
        }

        [Theory]
        [InlineData(HttpStatusCode.OK)]
        [InlineData(HttpStatusCode.Accepted)]
        [InlineData(HttpStatusCode.BadRequest)]
        [InlineData(HttpStatusCode.Unauthorized)]
        [InlineData(HttpStatusCode.NotFound)]
        [InlineData(HttpStatusCode.TooManyRequests)]
        public void OtherStatuses_AreNotUnavailable(HttpStatusCode status) =>
            Assert.Null(LHDNApiService.LhdnUnavailableMessage(status));
    }
}
