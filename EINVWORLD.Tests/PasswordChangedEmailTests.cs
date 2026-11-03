using System;
using EINVWORLD.Helpers;
using Xunit;

namespace EINVWORLD.Tests
{
    /// <summary>FINDINGS.md #11: the "your password was changed" notice.</summary>
    public class PasswordChangedEmailTests
    {
        [Fact]
        public void Body_Encodes_The_Name_And_Shows_Malaysia_Time()
        {
            var body = PasswordChangedEmail.Body("<script>x</script>", new DateTime(2026, 11, 3, 1, 30, 0, DateTimeKind.Utc));
            Assert.DoesNotContain("<script>", body);
            Assert.Contains("&lt;script&gt;", body);
            Assert.Contains("03 Nov 2026, 9:30 AM", body); // UTC+8
            Assert.Contains("Forgot password", body);
        }

        [Fact]
        public void Body_Without_A_Name_Still_Greets() =>
            Assert.Contains("Hello there", PasswordChangedEmail.Body(null, DateTime.UtcNow));
    }
}
