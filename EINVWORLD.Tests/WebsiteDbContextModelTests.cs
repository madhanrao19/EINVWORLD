using EINVWORLD.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EINVWORLD.Tests
{
    /// <summary>
    /// EF Core validates the model (keys, navigations, etc.) the first time DbContext.Model is
    /// accessed — no real database connection is required to trigger it. This guards against
    /// regressions like the one caught during the SEO/GEO redesign: adding a public List&lt;T&gt;
    /// property to ResourceItem (FaqItems) made EF try to map it as a navigation to a keyless
    /// entity, which throws InvalidOperationException at the first request that touches the
    /// DbContext — a failure mode unit tests using plain object construction never exercise.
    /// </summary>
    public class WebsiteDbContextModelTests
    {
        [Fact]
        public void Model_BuildsWithoutValidationErrors()
        {
            var options = new DbContextOptionsBuilder<WebsiteDbContext>()
                .UseSqlServer("Server=.;Database=EinvWorldModelValidationOnly;Trusted_Connection=True;")
                .Options;

            using var context = new WebsiteDbContext(options);

            // Accessing .Model triggers EF's model validation without opening a connection.
            var model = context.Model;

            Assert.NotNull(model);
        }
    }
}
