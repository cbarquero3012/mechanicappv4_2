namespace MechanicApp.Server.Options
{
    /// <summary>
    /// Configuration settings for sending email via SMTP (e.g., Gmail, SendGrid relay).
    /// Bind from the "Smtp" configuration section.
    /// </summary>
    public class SmtpSettings
    {
        public const string SectionName = "Smtp";

        /// <summary>SMTP server hostname (e.g., smtp.gmail.com).</summary>
        public string Host { get; set; } = "smtp.gmail.com";

        /// <summary>SMTP server port. Typically 587 (STARTTLS) or 465 (SSL).</summary>
        public int Port { get; set; } = 587;

        /// <summary>Login username / email address for SMTP authentication.</summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>Login password or app-specific password for SMTP authentication. Keep out of source control.</summary>
        public string Password { get; set; } = string.Empty;

        /// <summary>The "From" email address shown in outgoing messages.</summary>
        public string FromEmail { get; set; } = "noreply@mechanicapp.com";

        /// <summary>The display name shown alongside the From address.</summary>
        public string FromName { get; set; } = "MechanicApp";

        /// <summary>Whether to use SSL/TLS when connecting to the SMTP server.</summary>
        public bool EnableSsl { get; set; } = true;

        /// <summary>
        /// Public-facing base URL for login links in emails (e.g., "https://app.mechanicapp.com").
        /// Falls back to Request.Host if empty, but should be set in production.
        /// </summary>
        public string FrontendBaseUrl { get; set; } = string.Empty;
    }
}
