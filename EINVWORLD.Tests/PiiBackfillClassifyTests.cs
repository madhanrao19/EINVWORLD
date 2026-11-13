using System;
using System.IO;
using eInvWorld.Data;
using eInvWorld.Services.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using State = eInvWorld.Services.Security.PiiEncryptionBackfillService.ValueState;

namespace EINVWORLD.Tests
{
    /// <summary>How the backfill tells plaintext, current ciphertext, old-key ciphertext and unreadable ciphertext apart.</summary>
    public class PiiBackfillClassifyTests
    {
        private static ServiceProvider Ring(string dir)
        {
            var services = new ServiceCollection();
            services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(dir)).SetApplicationName("test");
            return services.BuildServiceProvider();
        }

        private static IDataProtector Pii(IServiceProvider sp) =>
            sp.GetRequiredService<IDataProtectionProvider>().CreateProtector(ApplicationDbContext.PiiProtectionPurpose);

        [Fact]
        public void Classify_TellsTheFourCasesApart()
        {
            var dir = Directory.CreateTempSubdirectory("einv-ring").FullName;
            try
            {
                string oldCipher;
                using (var first = Ring(dir))
                {
                    oldCipher = Pii(first).Protect("ACCT-1");
                    Assert.Equal(State.Current, PiiEncryptionBackfillService.Classify(Pii(first), oldCipher).State);
                    Assert.Equal((State.Plaintext, "ACCT-1"), PiiEncryptionBackfillService.Classify(Pii(first), "ACCT-1"));
                    // A newer key becomes the ring's current key (as Staging's own key did next to the copied one).
                    first.GetRequiredService<IKeyManager>().CreateNewKey(DateTimeOffset.UtcNow.AddSeconds(1), DateTimeOffset.UtcNow.AddDays(90));
                }
                System.Threading.Thread.Sleep(1500);

                using var second = Ring(dir); // a fresh provider reads the ring again
                var rekey = PiiEncryptionBackfillService.Classify(Pii(second), oldCipher);
                Assert.Equal((State.OldKey, "ACCT-1"), rekey);
                var newCipher = Pii(second).Protect(rekey.Plaintext!);
                Assert.Equal(State.Current, PiiEncryptionBackfillService.Classify(Pii(second), newCipher).State);

                // Ciphertext from a ring we don't hold is never treated as plaintext (it would be encrypted twice).
                var foreign = new EphemeralDataProtectionProvider().CreateProtector(ApplicationDbContext.PiiProtectionPurpose).Protect("ACCT-2");
                Assert.Equal(State.Unreadable, PiiEncryptionBackfillService.Classify(Pii(second), foreign).State);
            }
            finally
            {
                Directory.Delete(dir, recursive: true);
            }
        }

        // Production-like ring: application name "eInvWorld"; keys are plaintext files unless DPAPI is asked for.
        private static ServiceProvider ProductionLikeRing(string dir, bool dpapi)
        {
            var services = new ServiceCollection();
            var builder = services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(dir)).SetApplicationName("eInvWorld");
            if (dpapi && OperatingSystem.IsWindows()) builder.ProtectKeysWithDpapi(protectToLocalMachine: true);
            return services.BuildServiceProvider();
        }

        /// <summary>
        /// Rehearsal of docs/PRODUCTION-KEY-ROTATION-PLAN.md on a throwaway key ring: retire the plaintext key, let the
        /// app create an encrypted one, re-key, remove the old key, roll back. No value may become unreadable.
        /// </summary>
        [Fact]
        public void Rotation_Rehearsal_NoValueBecomesUnreadable_OldCookiesStopWorking_RollbackRestoresThem()
        {
            var dir = Directory.CreateTempSubdirectory("einv-rotation").FullName;
            var aside = Directory.CreateTempSubdirectory("einv-rotation-aside").FullName;
            try
            {
                // Today: one plaintext key protects a stored bank account number and a sign-in cookie.
                string bank, cookie;
                using (var today = ProductionLikeRing(dir, dpapi: false))
                {
                    bank = Pii(today).Protect("ACCT-PROD-1");
                    cookie = today.GetRequiredService<IDataProtectionProvider>().CreateProtector("auth-cookie").Protect("signed-in");
                }
                var oldKey = Assert.Single(Directory.GetFiles(dir, "key-*.xml"));
                Assert.Contains("masterKey", File.ReadAllText(oldKey));

                // Step 3: retire the key for new writes by its file (it stays in the ring, so everything stays readable).
                var xml = File.ReadAllText(oldKey);
                foreach (var (tag, daysAgo) in new[] { ("creationDate", 30), ("activationDate", 30), ("expirationDate", 1) })
                    xml = System.Text.RegularExpressions.Regex.Replace(xml, $"<{tag}>[^<]+</{tag}>",
                        $"<{tag}>{DateTimeOffset.UtcNow.AddDays(-daysAgo):o}</{tag}>");
                File.WriteAllText(oldKey, xml);

                // Steps 4-5: the restarted app creates a new key (encrypted at rest) and the re-key moves the value to it.
                string rekeyed;
                using (var restarted = ProductionLikeRing(dir, dpapi: true))
                {
                    Pii(restarted).Protect("first write after the restart");
                    var newKey = Assert.Single(Directory.GetFiles(dir, "key-*.xml"), f => f != oldKey);
                    if (OperatingSystem.IsWindows())
                    {
                        Assert.Contains("encryptedSecret", File.ReadAllText(newKey));
                        Assert.DoesNotContain("masterKey", File.ReadAllText(newKey));
                    }
                    var state = PiiEncryptionBackfillService.Classify(Pii(restarted), bank);
                    Assert.Equal((State.OldKey, "ACCT-PROD-1"), state);
                    rekeyed = Pii(restarted).Protect(state.Plaintext!);
                    Assert.Equal(State.Current, PiiEncryptionBackfillService.Classify(Pii(restarted), rekeyed).State);
                    // The old key is still in the ring: nobody is signed out yet.
                    Assert.Equal("signed-in", restarted.GetRequiredService<IDataProtectionProvider>().CreateProtector("auth-cookie").Unprotect(cookie));
                }

                // Step 7: the old key leaves the ring.
                var moved = Path.Combine(aside, Path.GetFileName(oldKey));
                File.Move(oldKey, moved);
                using (var rotated = ProductionLikeRing(dir, dpapi: true))
                {
                    Assert.Equal(State.Current, PiiEncryptionBackfillService.Classify(Pii(rotated), rekeyed).State);
                    Assert.Equal("ACCT-PROD-1", Pii(rotated).Unprotect(rekeyed));
                    // A value that had NOT been re-keyed is reported, never taken for plaintext and encrypted again.
                    Assert.Equal(State.Unreadable, PiiEncryptionBackfillService.Classify(Pii(rotated), bank).State);
                    // A cookie made (or forged) with the retired key is refused.
                    Assert.ThrowsAny<System.Security.Cryptography.CryptographicException>(() =>
                        rotated.GetRequiredService<IDataProtectionProvider>().CreateProtector("auth-cookie").Unprotect(cookie));
                }

                // Rollback: putting the key file back restores old cookies and keeps the re-keyed value readable.
                File.Move(moved, oldKey);
                using var rolledBack = ProductionLikeRing(dir, dpapi: true);
                Assert.Equal("signed-in", rolledBack.GetRequiredService<IDataProtectionProvider>().CreateProtector("auth-cookie").Unprotect(cookie));
                Assert.Equal("ACCT-PROD-1", Pii(rolledBack).Unprotect(rekeyed));
                Assert.Equal("ACCT-PROD-1", Pii(rolledBack).Unprotect(bank));
            }
            finally
            {
                Directory.Delete(dir, recursive: true);
                Directory.Delete(aside, recursive: true);
            }
        }
    }
}
