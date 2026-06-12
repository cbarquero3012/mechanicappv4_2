
// ─────────────────────────────────────────────────────────────────────────────
// SubscriptionPaymentLinkTests
// ─────────────────────────────────────────────────────────────────────────────
//
//  SubscriptionGetConfigTests        — verifies that GetConfig returns the three
//      per-plan payment link URLs (standard, premium, platinum).
//
//  SubscriptionBuildPaymentUrlTests  — verifies that Onboard returns the correct
//      plan-specific paymentUrl in its response, including the prefilled_email
//      query-string parameter, and that an unknown/golden plan falls back to the
//      legacy PaymentLinkUrl.
//
// ─────────────────────────────────────────────────────────────────────────────

using System.Text.Json;
using MechanicApp.Server.Controllers;
using MechanicApp.Server.Models;
using MechanicApp.Server.Options;
using MechanicApp.Server.Services;
using MechanicApp.Tests.Fakes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace MechanicApp.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// GetConfig — per-plan payment links
// ─────────────────────────────────────────────────────────────────────────────
public class SubscriptionGetConfigTests
{
    private static SubscriptionController Build(StripeSettings stripe)
    {
        var db             = Substitute.For<IDbService>();
        var provisioning   = Substitute.For<ITenantProvisioningService>();
        var tenantCtx      = Substitute.For<ITenantContext>();
        var emailService   = new FakeEmailService();
        var smtp           = Options.Create(new EmailSettings { FrontendBaseUrl = "https://app.test" });
        var logger         = NullLogger<SubscriptionController>.Instance;

        return new SubscriptionController(
            db,
            Options.Create(stripe),
            provisioning,
            tenantCtx,
            emailService,
            smtp,
            logger);
    }

    [Fact]
    public void GetConfig_Returns_PaymentLinks_Object()
    {
        var stripe = new StripeSettings
        {
            StandardPaymentLinkUrl = "https://buy.stripe.com/standard",
            PremiumPaymentLinkUrl  = "https://buy.stripe.com/premium",
            PlatinumPaymentLinkUrl = "https://buy.stripe.com/platinum"
        };

        var controller = Build(stripe);
        var result     = controller.GetConfig();

        var ok = Assert.IsType<OkObjectResult>(result);
        var json = JsonSerializer.Serialize(ok.Value);
        using var doc = JsonDocument.Parse(json);
        var links = doc.RootElement.GetProperty("paymentLinks");

        Assert.Equal("https://buy.stripe.com/standard", links.GetProperty("standard").GetString());
        Assert.Equal("https://buy.stripe.com/premium",  links.GetProperty("premium").GetString());
        Assert.Equal("https://buy.stripe.com/platinum", links.GetProperty("platinum").GetString());
    }

    [Fact]
    public void GetConfig_Returns_EmptyStrings_WhenLinksNotConfigured()
    {
        var stripe = new StripeSettings(); // all links empty

        var controller = Build(stripe);
        var result     = controller.GetConfig();

        var ok = Assert.IsType<OkObjectResult>(result);
        var json = JsonSerializer.Serialize(ok.Value);
        using var doc = JsonDocument.Parse(json);
        var links = doc.RootElement.GetProperty("paymentLinks");

        Assert.Equal(string.Empty, links.GetProperty("standard").GetString());
        Assert.Equal(string.Empty, links.GetProperty("premium").GetString());
        Assert.Equal(string.Empty, links.GetProperty("platinum").GetString());
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Onboard — BuildPaymentUrl routing per plan
// ─────────────────────────────────────────────────────────────────────────────
public class SubscriptionBuildPaymentUrlTests
{
    private static (SubscriptionController controller, FakeEmailService email) Build(
        StripeSettings stripe,
        ITenantProvisioningService provisioning,
        IDbService db)
    {
        var emailService = new FakeEmailService();
        var smtp         = Options.Create(new EmailSettings { FrontendBaseUrl = "https://app.test" });
        var tenantCtx    = Substitute.For<ITenantContext>();
        var logger       = NullLogger<SubscriptionController>.Instance;

        return (new SubscriptionController(
            db,
            Options.Create(stripe),
            provisioning,
            tenantCtx,
            emailService,
            smtp,
            logger), emailService);
    }

    private static StripeSettings DefaultStripe() => new()
    {
        PaymentLinkUrl         = "https://buy.stripe.com/fallback",
        StandardPaymentLinkUrl = "https://buy.stripe.com/standard",
        PremiumPaymentLinkUrl  = "https://buy.stripe.com/premium",
        PlatinumPaymentLinkUrl = "https://buy.stripe.com/platinum"
    };

    // ── Standard plan ────────────────────────────────────────────────────────

    [Fact]
    public async Task Onboard_Standard_Returns_StandardPaymentUrl()
    {
        var provisioning = Substitute.For<ITenantProvisioningService>();
        var db           = Substitute.For<IDbService>();

        provisioning.GetTenantByEmailAsync(Arg.Any<string>()).Returns((Tenant?)null);
        provisioning.ProvisionTenantAsync("Shop", "owner@shop.com", "standard")
                    .Returns(new Tenant { Slug = "shop", DatabaseName = "shop_db", PlanName = "standard" });

        var (controller, _) = Build(DefaultStripe(), provisioning, db);

        var req = new TenantOnboardRequest
        {
            Email = "owner@shop.com", CompanyName = "Shop", AdminPassword = "Pass1!", PlanName = "standard"
        };

        var result = await controller.Onboard(req);

        var ok      = Assert.IsType<OkObjectResult>(result);
        var json    = JsonSerializer.Serialize(ok.Value);
        using var doc = JsonDocument.Parse(json);
        var paymentUrl = doc.RootElement.GetProperty("paymentUrl").GetString();

        Assert.NotNull(paymentUrl);
        Assert.StartsWith("https://buy.stripe.com/standard", paymentUrl);
        Assert.Contains("prefilled_email=owner%40shop.com", paymentUrl);
    }

    // ── Premium plan ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Onboard_Premium_Returns_PremiumPaymentUrl()
    {
        var provisioning = Substitute.For<ITenantProvisioningService>();
        var db           = Substitute.For<IDbService>();

        provisioning.GetTenantByEmailAsync(Arg.Any<string>()).Returns((Tenant?)null);
        provisioning.ProvisionTenantAsync("Shop", "owner@shop.com", "premium")
                    .Returns(new Tenant { Slug = "shop", DatabaseName = "shop_db", PlanName = "premium" });

        var (controller, _) = Build(DefaultStripe(), provisioning, db);

        var req = new TenantOnboardRequest
        {
            Email = "owner@shop.com", CompanyName = "Shop", AdminPassword = "Pass1!", PlanName = "premium"
        };

        var result     = await controller.Onboard(req);
        var ok         = Assert.IsType<OkObjectResult>(result);
        var json       = JsonSerializer.Serialize(ok.Value);
        using var doc  = JsonDocument.Parse(json);
        var paymentUrl = doc.RootElement.GetProperty("paymentUrl").GetString();

        Assert.NotNull(paymentUrl);
        Assert.StartsWith("https://buy.stripe.com/premium", paymentUrl);
        Assert.Contains("prefilled_email=owner%40shop.com", paymentUrl);
    }

    // ── Platinum plan ────────────────────────────────────────────────────────

    [Fact]
    public async Task Onboard_Platinum_Returns_PlatinumPaymentUrl()
    {
        var provisioning = Substitute.For<ITenantProvisioningService>();
        var db           = Substitute.For<IDbService>();

        provisioning.GetTenantByEmailAsync(Arg.Any<string>()).Returns((Tenant?)null);
        provisioning.ProvisionTenantAsync("Shop", "owner@shop.com", "platinum")
                    .Returns(new Tenant { Slug = "shop", DatabaseName = "shop_db", PlanName = "platinum" });

        var (controller, _) = Build(DefaultStripe(), provisioning, db);

        var req = new TenantOnboardRequest
        {
            Email = "owner@shop.com", CompanyName = "Shop", AdminPassword = "Pass1!", PlanName = "platinum"
        };

        var result     = await controller.Onboard(req);
        var ok         = Assert.IsType<OkObjectResult>(result);
        var json       = JsonSerializer.Serialize(ok.Value);
        using var doc  = JsonDocument.Parse(json);
        var paymentUrl = doc.RootElement.GetProperty("paymentUrl").GetString();

        Assert.NotNull(paymentUrl);
        Assert.StartsWith("https://buy.stripe.com/platinum", paymentUrl);
        Assert.Contains("prefilled_email=owner%40shop.com", paymentUrl);
    }

    // ── Unknown / Golden plan → legacy fallback ──────────────────────────────

    [Fact]
    public async Task Onboard_UnknownPlan_Falls_Back_To_LegacyPaymentLinkUrl()
    {
        var provisioning = Substitute.For<ITenantProvisioningService>();
        var db           = Substitute.For<IDbService>();

        provisioning.GetTenantByEmailAsync(Arg.Any<string>()).Returns((Tenant?)null);
        provisioning.ProvisionTenantAsync("Shop", "owner@shop.com", "golden")
                    .Returns(new Tenant { Slug = "shop", DatabaseName = "shop_db", PlanName = "golden" });

        var (controller, _) = Build(DefaultStripe(), provisioning, db);

        var req = new TenantOnboardRequest
        {
            Email = "owner@shop.com", CompanyName = "Shop", AdminPassword = "Pass1!", PlanName = "golden"
        };

        var result     = await controller.Onboard(req);
        var ok         = Assert.IsType<OkObjectResult>(result);
        var json       = JsonSerializer.Serialize(ok.Value);
        using var doc  = JsonDocument.Parse(json);
        var paymentUrl = doc.RootElement.GetProperty("paymentUrl").GetString();

        Assert.NotNull(paymentUrl);
        Assert.StartsWith("https://buy.stripe.com/fallback", paymentUrl);
    }

    // ── No plan-specific URL configured → empty string ───────────────────────

    [Fact]
    public async Task Onboard_Standard_ReturnsEmpty_WhenNoLinkConfigured()
    {
        var provisioning = Substitute.For<ITenantProvisioningService>();
        var db           = Substitute.For<IDbService>();

        provisioning.GetTenantByEmailAsync(Arg.Any<string>()).Returns((Tenant?)null);
        provisioning.ProvisionTenantAsync("Shop", "owner@shop.com", "standard")
                    .Returns(new Tenant { Slug = "shop", DatabaseName = "shop_db", PlanName = "standard" });

        // All payment links intentionally unconfigured
        var stripe = new StripeSettings();
        var (controller, _) = Build(stripe, provisioning, db);

        var req = new TenantOnboardRequest
        {
            Email = "owner@shop.com", CompanyName = "Shop", AdminPassword = "Pass1!", PlanName = "standard"
        };

        var result     = await controller.Onboard(req);
        var ok         = Assert.IsType<OkObjectResult>(result);
        var json       = JsonSerializer.Serialize(ok.Value);
        using var doc  = JsonDocument.Parse(json);
        var paymentUrl = doc.RootElement.GetProperty("paymentUrl").GetString();

        Assert.Equal(string.Empty, paymentUrl);
    }
}
