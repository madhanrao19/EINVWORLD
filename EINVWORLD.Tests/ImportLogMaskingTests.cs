using System;
using System.Reflection;
using System.Threading.Tasks;
using eInvWorld.Models;
using eInvWorld.Services;
using EINVWORLD.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace EINVWORLD.Tests
{
    /// <summary>
    /// The LHDN import result is logged ("[LHDN Import] Result for TIN …: {Result}") and stored on the SyncJob, so it
    /// must never carry the full TIN (an IG TIN identifies a person).
    /// </summary>
    public class ImportLogMaskingTests
    {
        /// <summary>Interface fake: every call returns null / default / a completed task with a default result.</summary>
        public class NullProxy : DispatchProxy
        {
            public static T Create<T>() where T : class => DispatchProxy.Create<T, NullProxy>();

            protected override object? Invoke(MethodInfo? method, object?[]? args)
            {
                var t = method!.ReturnType;
                if (t == typeof(Task)) return Task.CompletedTask;
                if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Task<>))
                {
                    var r = t.GetGenericArguments()[0];
                    return typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(r)
                        .Invoke(null, new[] { r.IsValueType ? Activator.CreateInstance(r) : null });
                }
                return t.IsValueType && t != typeof(void) ? Activator.CreateInstance(t) : null;
            }
        }

        [Fact]
        public async Task No_documents_result_masks_the_tin()
        {
            const string tin = "IG20123456789";
            var helper = new InvoiceSyncHelper(null!, NullProxy.Create<ILHDNApiService>(), null!, null!,
                NullProxy.Create<ITokenService>(), null!, null!, NullLogger<InvoiceSyncHelper>.Instance,
                Options.Create(new FilePathConfig()), null!);

            var result = await helper.RunFullImportFromLhdnAsync(tin);

            Assert.StartsWith("No documents found", result);
            Assert.DoesNotContain(tin, result);
            Assert.Contains(LogSanitizer.MaskTin(tin), result);
        }
    }
}
