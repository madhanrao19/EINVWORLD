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
    }
}
