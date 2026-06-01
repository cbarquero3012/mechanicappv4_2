using MechanicApp.Server.Services;

namespace MechanicApp.Tests.Fakes;

/// <summary>
/// In-memory implementation of <see cref="IEmailService"/> that records the
/// arguments of the last <see cref="SendWelcomeEmailAsync"/> call.
/// Use this to verify controller dispatch behaviour without any SMTP dependency.
/// </summary>
internal sealed class FakeEmailService : IEmailService
{
    public bool      WasCalled    { get; private set; }
    public string?   LastToEmail  { get; private set; }
    public string?   LastUsername { get; private set; }
    public string?   LastLoginUrl { get; private set; }
    public string?   LastPlanName { get; private set; }
    public string?   LastPassword { get; private set; }
    public DateTime? LastExpiresAt{ get; private set; }
    public bool      LastIsDemo   { get; private set; }

    public Task<bool> SendWelcomeEmailAsync(
        string toEmail, string username, string loginUrl, string planName,
        string? password = null, DateTime? expiresAt = null, bool isDemo = false)
    {
        WasCalled     = true;
        LastToEmail   = toEmail;
        LastUsername  = username;
        LastLoginUrl  = loginUrl;
        LastPlanName  = planName;
        LastPassword  = password;
        LastExpiresAt = expiresAt;
        LastIsDemo    = isDemo;
        return Task.FromResult(true);
    }
}
