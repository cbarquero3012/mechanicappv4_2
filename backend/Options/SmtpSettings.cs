namespace MechanicApp.Server.Options
{
    /// <summary>
    /// Configuration settings for sending transactional email via the Resend API.
    /// Bind from the "Email" configuration section.
    /// Set credentials through environment variables — never commit them to source control.
    /// </summary>
    public class EmailSettings
    {
        public const string SectionName = "Email";

        /// <summary>
        /// Resend API key. Obtain from https://resend.com/api-keys.
        /// Set via environment variable: Email__ResendApiKey
        /// </summary>
        public string ResendApiKey { get; set; } = string.Empty;

        /// <summary>The "From" email address shown in outgoing messages. Must be a verified domain in Resend.</summary>
        public string FromEmail { get; set; } = "support@mechanicapp.cloud";

        /// <summary>The display name shown alongside the From address.</summary>
        public string FromName { get; set; } = "MechanicApp";

        /// <summary>
        /// Public-facing base URL for login links in emails (e.g., "https://app.mechanicapp.com").
        /// Must not have a trailing slash.
        /// </summary>
        public string FrontendBaseUrl { get; set; } = string.Empty;
    }
}
