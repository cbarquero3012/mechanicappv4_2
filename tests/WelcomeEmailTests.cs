// ─────────────────────────────────────────────────────────────────────────────
// CONCEPT TESTS — Welcome Email Feature
// ─────────────────────────────────────────────────────────────────────────────
//
//  WelcomeEmailContentTests  — verifies that EmailService builds the correct
//      subject, HTML badge colour, password row, and expiry row for every
//      account type. Uses CaptureEmailService (in-process, no SMTP).
//
//  DemoEmailDispatchTests    — verifies that DemoController.CreateDemo calls
//      IEmailService with the right arguments. Uses FakeEmailService +
//      NSubstitute mocks for ITenantProvisioningService.
//
// ─────────────────────────────────────────────────────────────────────────────

using MechanicApp.Server.Controllers;
using MechanicApp.Server.Models;
using MechanicApp.Server.Options;
using MechanicApp.Server.Services;
using MechanicApp.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace MechanicApp.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// EmailService — HTML content tests
// ─────────────────────────────────────────────────────────────────────────────
public class WelcomeEmailContentTests
{
    /// Shared factory: SmtpSettings with no host so the real SMTP path is
    /// never reached. The CaptureEmailService override captures before that.
    private static CaptureEmailService BuildService() =>
        new(Options.Create(new SmtpSettings()), NullLogger<EmailService>.Instance);

    // ── Demo account ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Demo_Subject_Contains_FreeTrial()
    {
        var svc = BuildService();
        await svc.SendWelcomeEmailAsync(
            "owner@garage.com", "johndoe", "https://app.test/shop/login",
            "free-trial", password: "Pass123!", expiresAt: new DateTime(2026, 5, 28, 0, 0, 0, DateTimeKind.Utc),
            isDemo: true);

        Assert.Contains("Free Trial", svc.CapturedSubject, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Demo_Body_Has_AmberBadge_And_FreeTrial_Label()
    {
        var svc = BuildService();
        await svc.SendWelcomeEmailAsync(
            "owner@garage.com", "johndoe", "https://app.test/shop/login",
            "free-trial", password: "Pass123!", expiresAt: DateTime.UtcNow.AddDays(7),
            isDemo: true);

        Assert.Contains("#f59e0b", svc.CapturedBody);   // amber badge colour
        Assert.Contains("FREE TRIAL", svc.CapturedBody);
    }

    [Fact]
    public async Task Demo_Body_Shows_Password_And_SecurityWarning()
    {
        var svc = BuildService();
        await svc.SendWelcomeEmailAsync(
            "owner@garage.com", "johndoe", "https://app.test/shop/login",
            "free-trial", password: "SecureAbc9!", expiresAt: DateTime.UtcNow.AddDays(7),
            isDemo: true);

        Assert.Contains("SecureAbc9!", svc.CapturedBody);
        Assert.Contains("Change your password after first login", svc.CapturedBody);
    }

    [Fact]
    public async Task Demo_Body_Shows_TrialExpiry_Date()
    {
        var svc = BuildService();
        var expiry = new DateTime(2026, 5, 28, 0, 0, 0, DateTimeKind.Utc);

        await svc.SendWelcomeEmailAsync(
            "owner@garage.com", "johndoe", "https://app.test/shop/login",
            "free-trial", password: "Pass123!", expiresAt: expiry,
            isDemo: true);

        Assert.Contains("Trial expires", svc.CapturedBody);
        Assert.Contains("May 28, 2026", svc.CapturedBody);
    }

    // ── Paid onboard (password known at creation time) ───────────────────────

    [Fact]
    public async Task PaidOnboard_Subject_Contains_AccountCreated()
    {
        var svc = BuildService();
        await svc.SendWelcomeEmailAsync(
            "owner@garage.com", "johndoe", "https://app.test/shop/login",
            "professional", password: "MyP@ss1!", expiresAt: DateTime.UtcNow.AddDays(30));

        Assert.Contains("Account Has Been Created", svc.CapturedSubject, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PaidOnboard_Body_Has_BlueBadge_And_PlanName()
    {
        var svc = BuildService();
        await svc.SendWelcomeEmailAsync(
            "owner@garage.com", "johndoe", "https://app.test/shop/login",
            "professional", password: "MyP@ss1!", expiresAt: DateTime.UtcNow.AddDays(30));

        Assert.Contains("#2563eb", svc.CapturedBody);        // blue badge colour
        Assert.Contains("PROFESSIONAL", svc.CapturedBody);  // plan name uppercased
    }

    [Fact]
    public async Task PaidOnboard_Body_Shows_Password_And_ActiveUntil()
    {
        var svc = BuildService();
        var expiry = new DateTime(2026, 6, 20, 0, 0, 0, DateTimeKind.Utc);

        await svc.SendWelcomeEmailAsync(
            "owner@garage.com", "johndoe", "https://app.test/shop/login",
            "professional", password: "MyP@ss1!", expiresAt: expiry);

        Assert.Contains("MyP@ss1!", svc.CapturedBody);
        Assert.Contains("Active until", svc.CapturedBody);
        Assert.Contains("June 20, 2026", svc.CapturedBody);
    }

    // ── Stripe webhook (password is already hashed — not available) ──────────

    [Fact]
    public async Task StripeWebhook_Subject_Contains_PaymentConfirmed()
    {
        var svc = BuildService();
        await svc.SendWelcomeEmailAsync(
            "owner@garage.com", "johndoe", "https://app.test/shop/login",
            "professional", password: null, expiresAt: DateTime.UtcNow.AddDays(30));

        Assert.Contains("Payment Is Confirmed", svc.CapturedSubject, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StripeWebhook_Body_Has_No_PasswordRow()
    {
        var svc = BuildService();
        await svc.SendWelcomeEmailAsync(
            "owner@garage.com", "johndoe", "https://app.test/shop/login",
            "professional", password: null, expiresAt: DateTime.UtcNow.AddDays(30));

        Assert.DoesNotContain("Change your password after first login", svc.CapturedBody);
    }

    // ── Edge cases ───────────────────────────────────────────────────────────

    [Fact]
    public async Task NoExpiresAt_Body_Has_No_ExpiryRow()
    {
        var svc = BuildService();
        await svc.SendWelcomeEmailAsync(
            "owner@garage.com", "johndoe", "https://app.test/shop/login",
            "professional");

        Assert.DoesNotContain("Active until", svc.CapturedBody);
        Assert.DoesNotContain("Trial expires", svc.CapturedBody);
    }

    [Fact]
    public async Task InvalidEmail_Returns_False_And_Never_Builds_Content()
    {
        var svc = BuildService();

        var result = await svc.SendWelcomeEmailAsync(
            "not-an-email", "johndoe", "https://app.test/shop/login", "free-trial",
            password: "Pass123!");

        Assert.False(result);
        Assert.Null(svc.CapturedSubject); // SendEmailWithRetryAsync was never reached
    }

    [Fact]
    public async Task EmptyEmail_Returns_False_And_Never_Builds_Content()
    {
        var svc = BuildService();

        var result = await svc.SendWelcomeEmailAsync(
            "", "johndoe", "https://app.test/shop/login", "free-trial");

        Assert.False(result);
        Assert.Null(svc.CapturedSubject);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// DemoController — email dispatch tests
// ─────────────────────────────────────────────────────────────────────────────
public class DemoEmailDispatchTests
{
    private static (DemoController controller, FakeEmailService email) Build(
        ITenantProvisioningService provisioning)
    {
        var email = new FakeEmailService();
        var smtp = Options.Create(new SmtpSettings { FrontendBaseUrl = "https://app.mechanicapp.com" });
        return (new DemoController(provisioning, email, smtp), email);
    }

    [Fact]
    public async Task CreateDemo_Sends_WelcomeEmail_With_CorrectDemoParams()
    {
        // Arrange
        var provisioning = Substitute.For<ITenantProvisioningService>();
        var (controller, email) = Build(provisioning);

        var tenant = new Tenant
        {
            Slug = "my-shop",
            DatabaseName = "my_shop_db",
            IsDemo = true,
            Status = "active",
            DemoExpiresAt = DateTime.UtcNow.AddDays(7),
            PlanName = "free-trial"
        };

        provisioning.GetTenantByEmailAsync("owner@garage.com")
                    .Returns((Tenant?)null);
        provisioning.ProvisionTenantAsync(
                        Arg.Any<string>(), "owner@garage.com", "demo", true, Arg.Any<string?>())
                    .Returns(tenant);

        var req = new CreateDemoRequest
        {
            Name = "My Garage",
            Email = "owner@garage.com",
            Username = "admin"
        };

        // Act
        await controller.CreateDemo(req);

        // Assert
        Assert.True(email.WasCalled, "SendWelcomeEmailAsync was never called");
        Assert.Equal("owner@garage.com", email.LastToEmail);
        Assert.Equal("admin", email.LastUsername);
        Assert.True(email.LastIsDemo, "isDemo should be true for demo accounts");
        Assert.NotNull(email.LastPassword);
        Assert.NotEmpty(email.LastPassword);
        Assert.NotNull(email.LastExpiresAt);
        Assert.Equal("free-trial", email.LastPlanName);
        Assert.Contains("my-shop/login", email.LastLoginUrl);
    }

    [Fact]
    public async Task CreateDemo_ExistingDemo_DoesNot_SendEmail()
    {
        // Arrange — the provisioning service finds an existing demo
        var provisioning = Substitute.For<ITenantProvisioningService>();
        var (controller, email) = Build(provisioning);

        var existing = new Tenant
        {
            Slug = "existing-shop",
            DatabaseName = "existing_db",
            IsDemo = true,
            Status = "active",
            DemoExpiresAt = DateTime.UtcNow.AddDays(3)
        };

        provisioning.GetTenantByEmailAsync("repeat@user.com").Returns(existing);

        var req = new CreateDemoRequest { Name = "Repeat", Email = "repeat@user.com" };

        // Act
        await controller.CreateDemo(req);

        // Assert
        Assert.False(email.WasCalled,
            "Email must NOT be sent when a demo already exists for this address");
    }
}
