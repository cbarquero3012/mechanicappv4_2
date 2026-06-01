using MechanicApp.Server.Options;
using MechanicApp.Server.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MechanicApp.Tests.Fakes;

/// <summary>
/// Subclass of <see cref="EmailService"/> that intercepts
/// <see cref="EmailService.SendEmailWithRetryAsync"/> so tests can inspect the
/// generated subject and HTML body without needing a live SMTP server.
/// </summary>
internal sealed class CaptureEmailService : EmailService
{
    public string? CapturedToEmail  { get; private set; }
    public string? CapturedSubject  { get; private set; }
    public string? CapturedBody     { get; private set; }

    public CaptureEmailService(IOptions<SmtpSettings> smtp, ILogger<EmailService> logger)
        : base(smtp, logger) { }

    protected override Task<bool> SendEmailWithRetryAsync(
        string toEmail, string subject, string htmlBody)
    {
        CapturedToEmail = toEmail;
        CapturedSubject = subject;
        CapturedBody    = htmlBody;
        return Task.FromResult(true);
    }
}
