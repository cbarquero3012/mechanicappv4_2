using MechanicApp.Server.Options;
using MechanicApp.Server.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MechanicApp.Tests.Fakes;

/// <summary>
/// Subclass of <see cref="EmailService"/> that intercepts
/// <see cref="EmailService.SendEmailWithRetryAsync"/> so tests can inspect the
/// generated subject and HTML body without hitting the Resend API.
/// </summary>
internal sealed class CaptureEmailService : EmailService
{
    public string? CapturedToEmail  { get; private set; }
    public string? CapturedSubject  { get; private set; }
    public string? CapturedBody     { get; private set; }

    /// <param name="settings">Email settings (API key not required — send is intercepted).</param>
    /// <param name="logger">Logger instance.</param>
    public CaptureEmailService(IOptions<EmailSettings> settings, ILogger<EmailService> logger)
        : base(settings, null!, logger) { }

    protected override Task<bool> SendEmailWithRetryAsync(
        string toEmail, string subject, string htmlBody)
    {
        CapturedToEmail = toEmail;
        CapturedSubject = subject;
        CapturedBody    = htmlBody;
        return Task.FromResult(true);
    }
}
