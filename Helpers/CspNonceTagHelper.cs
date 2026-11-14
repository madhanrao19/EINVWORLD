using eInvWorld.Services.Security;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace eInvWorld.Helpers
{
    /// <summary>
    /// Adds this response's Content-Security-Policy nonce to every <c>&lt;script&gt;</c> and <c>&lt;style&gt;</c> element a
    /// Razor view renders, so inline blocks keep running once the policy has no <c>'unsafe-inline'</c>. Registered in
    /// every <c>_ViewImports.cshtml</c>. Markup that bypasses Razor (strings built in JavaScript) gets no nonce.
    /// </summary>
    [HtmlTargetElement("script")]
    [HtmlTargetElement("style")]
    public sealed class CspNonceTagHelper : TagHelper
    {
        /// <summary>Set by Razor.</summary>
        [ViewContext]
        [HtmlAttributeNotBound]
        public ViewContext ViewContext { get; set; } = null!;

        // Run after other tag helpers so a nonce is never dropped by one that rewrites the element.
        public override int Order => 1000;

        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            if (output.Attributes.ContainsName("nonce")) return;
            output.Attributes.SetAttribute("nonce", ContentSecurityPolicy.GetNonce(ViewContext.HttpContext));
        }
    }
}
