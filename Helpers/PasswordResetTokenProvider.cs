using System;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EINVWORLD.Helpers
{
    /// <summary>
    /// Password-reset links live 2 hours instead of Identity's 1 day. A separate provider so email-confirmation
    /// links (the default provider) keep their day: people confirm later, but a reset link sitting in a mailbox
    /// is a standing way into the account.
    /// </summary>
    public sealed class PasswordResetTokenProvider<TUser> : DataProtectorTokenProvider<TUser> where TUser : class
    {
        public const string ProviderName = "PasswordReset";

        public PasswordResetTokenProvider(IDataProtectionProvider dataProtectionProvider,
            IOptions<PasswordResetTokenProviderOptions> options, ILogger<DataProtectorTokenProvider<TUser>> logger)
            : base(dataProtectionProvider, options, logger) { }
    }

    public sealed class PasswordResetTokenProviderOptions : DataProtectionTokenProviderOptions
    {
        public PasswordResetTokenProviderOptions()
        {
            Name = PasswordResetTokenProvider<object>.ProviderName;
            TokenLifespan = TimeSpan.FromHours(2);
        }
    }
}
