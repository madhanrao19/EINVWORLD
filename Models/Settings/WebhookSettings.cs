namespace eInvWorld.Models.Settings
{
    /// <summary>
    /// Controls the outbound webhook subsystem that notifies customer ERPs when an invoice reaches a
    /// terminal LHDN status (Valid / Cancelled / Rejected / Invalid). Bound from the configuration section
    /// <c>Webhooks</c>.
    /// <para>
    /// Default is OFF: while disabled the background dispatcher never scans or enqueues deliveries, so the
    /// system behaves exactly as before (customers are notified by email only). Turn it on by setting
    /// <c>Webhooks:Enabled = true</c> and registering at least one subscription in Admin → Webhooks.
    /// </para>
    /// </summary>
    public class WebhookSettings
    {
        /// <summary>Master switch. When false, no webhook scanning or delivery happens.</summary>
        public bool Enabled { get; set; } = false;

        /// <summary>Per-delivery HTTP timeout (seconds) for the POST to a customer's callback URL.</summary>
        public int DeliveryTimeoutSeconds { get; set; } = 15;

        /// <summary>
        /// When true (default), callback URLs that resolve to loopback/private/link-local hosts are
        /// rejected, mitigating SSRF via an attacker-supplied URL. Set false only for trusted on-prem
        /// receivers on a private network that legitimately live on RFC-1918 addresses.
        /// </summary>
        public bool BlockPrivateNetworks { get; set; } = true;

        /// <summary>
        /// When true (default), callback URLs must use HTTPS. Set false only for a trusted internal
        /// receiver reached over plain HTTP on a private network.
        /// </summary>
        public bool RequireHttps { get; set; } = true;
    }
}
