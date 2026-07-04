using System;
using eInvWorld.Services.Security;
using Microsoft.AspNetCore.DataProtection;
using Xunit;

namespace EINVWORLD.Tests
{
    /// <summary>
    /// Covers the field-level PII encryption converter: round-trip through the DataProtection key-ring,
    /// non-deterministic ciphertext, and the lenient read that keeps legacy plaintext (un-backfilled) rows
    /// readable — the property the one-time backfill relies on to be safely re-runnable.
    /// </summary>
    public class ProtectedStringConverterTests
    {
        private static readonly IDataProtector Protector =
            new EphemeralDataProtectionProvider().CreateProtector("eInvWorld.Pii.Test");

        private static ProtectedStringConverter Converter() => new(Protector);

        private static string Store(ProtectedStringConverter c, string plaintext) =>
            (string)c.ConvertToProvider(plaintext)!;

        private static string Read(ProtectedStringConverter c, string stored) =>
            (string)c.ConvertFromProvider(stored)!;

        [Fact]
        public void RoundTrips_PlaintextThroughCiphertextAndBack()
        {
            var c = Converter();
            const string plaintext = "1234567890-MYBANK";

            var stored = Store(c, plaintext);

            Assert.NotEqual(plaintext, stored);              // stored value is encrypted, not the input
            Assert.Equal(plaintext, Read(c, stored));        // decrypts back to the original
        }

        [Fact]
        public void Ciphertext_IsNonDeterministic_ButBothDecrypt()
        {
            var c = Converter();
            const string plaintext = "Unit 5, Level 12";

            var a = Store(c, plaintext);
            var b = Store(c, plaintext);

            Assert.NotEqual(a, b);                           // DataProtection is non-deterministic
            Assert.Equal(plaintext, Read(c, a));
            Assert.Equal(plaintext, Read(c, b));
        }

        [Fact]
        public void Read_LegacyPlaintext_ReturnedVerbatim()
        {
            // A row written before encryption was enabled: the stored value is raw plaintext, not our
            // ciphertext. The lenient read must return it unchanged so the app keeps working pre-backfill.
            var c = Converter();
            const string legacy = "Old Street, Kuala Lumpur";

            Assert.Equal(legacy, Read(c, legacy));
        }

        [Fact]
        public void Read_NonBase64Garbage_ReturnedVerbatim()
        {
            var c = Converter();
            const string garbage = "!!! not base64 !!!";

            Assert.Equal(garbage, Read(c, garbage));
        }

        [Fact]
        public void Read_Empty_ReturnedVerbatim()
        {
            var c = Converter();
            Assert.Equal(string.Empty, Read(c, string.Empty));
        }

        [Fact]
        public void CiphertextFromOtherPurpose_IsTreatedAsPlaintext_NotThrown()
        {
            // Ciphertext produced under a different DataProtection purpose cannot be unprotected by ours;
            // the converter must fall back to returning it verbatim rather than throwing on read.
            var other = new EphemeralDataProtectionProvider().CreateProtector("some.other.purpose");
            var foreign = other.Protect("secret");

            var c = Converter();
            Assert.Equal(foreign, Read(c, foreign));
        }
    }
}
