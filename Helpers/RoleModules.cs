namespace EINVWORLD.Helpers
{
    /// <summary>
    /// Fixed registry of app "modules" (top-level feature areas) that Role Management can grant or
    /// restrict per role. A plain array, not a DB table — this list changes only when a whole new
    /// feature area is added to the app, which is a code change anyway.
    /// </summary>
    public static class RoleModules
    {
        public sealed record Module(string Key, string DisplayName, string PathPrefix);

        public static readonly Module[] All =
        {
            new("Invoices", "Invoices", "/Invoices"),
            new("RecurringInvoices", "Recurring Invoices", "/RecurringInvoices"),
            new("Items", "Items", "/Items"),
            new("Templates", "Templates", "/Templates"),
            new("CompanyManagement", "Company Management", "/Suppliers"),
            new("Assistant", "E-Invoice Assistant", "/Assistant"),
        };

        /// <summary>The module (if any) that owns the given request path, by longest-prefix match.</summary>
        public static Module? ForPath(string path)
        {
            Module? best = null;
            foreach (var m in All)
            {
                if (path.StartsWith(m.PathPrefix, System.StringComparison.OrdinalIgnoreCase) &&
                    (best == null || m.PathPrefix.Length > best.PathPrefix.Length))
                {
                    best = m;
                }
            }
            return best;
        }
    }
}
