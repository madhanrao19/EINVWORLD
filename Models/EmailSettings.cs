namespace eInvWorld.Models
{
    public class EmailSettings
    {
        public string SmtpServer { get; set; } = default!;
        public int SmtpPort { get; set; }
        public string SmtpUsername { get; set; } = default!;
        public string SmtpPassword { get; set; } = default!;
        public string FromEmailName { get; set; } = default!;
        public string GlobalBccEmail { get; set; } = default!;
        public string DisallowedEmailDomains { get; set; } = default!;
    }
}
