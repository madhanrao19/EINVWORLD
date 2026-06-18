namespace eInvWorld.Models
{
    public class EmailConfiguration
    {
        public DefaultSettings Default { get; set; } = null!;
        public ContactUsEmailSettings ContactUsEmailSettings { get; set; } = null!;
    }

    public class DefaultSettings
    {
        public string SmtpServer { get; set; } = null!;
        public int SmtpPort { get; set; }
        public string SmtpUsername { get; set; } = null!;
        public string SmtpPassword { get; set; } = null!;
        public string FromEmailName { get; set; } = null!;
        public string DisallowedEmailDomains { get; set; } = null!;
        public string GlobalBccEmail { get; set; } = null!;
    }

    public class ContactUsEmailSettings
    {
        public string ReceiverEmail { get; set; } = null!;
        public string GlobalBccEmail { get; set; } = null!;
        public string AdminSubject { get; set; } = null!;
        public string UserSubject { get; set; } = null!;
    }
}
