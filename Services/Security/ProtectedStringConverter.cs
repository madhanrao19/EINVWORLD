using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace eInvWorld.Services.Security
{
    /// <summary>
    /// EF Core value converter that transparently encrypts a string column at rest using ASP.NET Core
    /// DataProtection (the same key-ring the app already provisions outside <c>App\</c>).
    /// <para>
    /// Writes always store ciphertext. Reads attempt to decrypt, but fall back to returning the raw stored
    /// value unchanged when it cannot be decrypted — e.g. a legacy plaintext row that the one-time backfill
    /// has not yet processed. This lenient read keeps the application fully functional <em>during</em> a
    /// partial backfill (mixed plaintext/ciphertext rows) and makes the backfill safely re-runnable
    /// (idempotent): an already-encrypted value decrypts to plaintext, is re-Protected, and lands as
    /// ciphertext again.
    /// </para>
    /// <para>
    /// Only the value is protected — no authenticated "purpose" binds the ciphertext to a specific column,
    /// so the DataProtection purpose string (see <see cref="eInvWorld.Data.ApplicationDbContext"/>) is the
    /// single isolation boundary for this class of PII. Losing the DataProtection key-ring makes these
    /// columns permanently unreadable, so the key-ring folder must be backed up (see SECRETS-SETUP.md).
    /// </para>
    /// </summary>
    public sealed class ProtectedStringConverter : ValueConverter<string, string>
    {
        /// <summary>
        /// Builds the converter over an already-purpose-scoped <see cref="IDataProtector"/>.
        /// </summary>
        public ProtectedStringConverter(IDataProtector protector)
            : base(
                plaintext => protector.Protect(plaintext),
                stored => Unprotect(protector, stored))
        {
        }

        private static string Unprotect(IDataProtector protector, string stored)
        {
            if (string.IsNullOrEmpty(stored))
                return stored;

            try
            {
                return protector.Unprotect(stored);
            }
            catch (CryptographicException)
            {
                // Not our ciphertext (legacy plaintext row, or a value written before encryption was
                // enabled). Return it verbatim so the app keeps working and the backfill can encrypt it.
                return stored;
            }
            catch (FormatException)
            {
                // Stored value isn't valid base64url, so it can't be our ciphertext — treat as plaintext.
                return stored;
            }
        }
    }
}
