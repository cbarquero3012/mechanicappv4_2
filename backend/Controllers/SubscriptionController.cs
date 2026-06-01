using System.Text.Json;
using MechanicApp.Server.Constants;
using MechanicApp.Server.Models;
using MechanicApp.Server.Options;
using MechanicApp.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Stripe;
using AppSubscription = MechanicApp.Server.Models.Subscription;

namespace MechanicApp.Server.Controllers
{
    /// <summary>
    /// Manages subscription status, Stripe webhook processing, and admin overrides.
    /// Now integrates with tenant provisioning for SaaS multi-tenancy.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class SubscriptionController(IDbService db, IOptions<StripeSettings> stripe, ITenantProvisioningService tenantProvisioning, ITenantContext tenantContext, ILogger<SubscriptionController> logger) : ControllerBase
    {
        private readonly StripeSettings _stripe = stripe.Value;

        // ────────────────────────────────────────────────────────
        // Public: Check current subscription status (used by frontend guard)
        // ────────────────────────────────────────────────────────
        [AllowAnonymous]
        [EnableRateLimiting("public")]
        [HttpGet("status")]
        public async Task<IActionResult> GetStatus()
        {
            // Demo tenants are always considered active (until expired)
            var tenant = tenantContext.CurrentTenant;
            if (tenant is { IsDemo: true })
            {
                var isExpired = tenant.DemoExpiresAt.HasValue && tenant.DemoExpiresAt < DateTime.UtcNow;
                if (!isExpired)
                {
                    return Ok(new
                    {
                        active = true,
                        status = "demo",
                        planName = "free-trial",
                        expiresAt = tenant.DemoExpiresAt,
                        isDemo = true,
                        daysRemaining = tenant.DemoExpiresAt.HasValue
                            ? (int)Math.Ceiling((tenant.DemoExpiresAt.Value - DateTime.UtcNow).TotalDays)
                            : 7
                    });
                }
            }

            var sub = await db.GetAsync<AppSubscription>(
                @"SELECT * FROM mechanic_db.""Subscriptions""
                  ORDER BY ""Id"" DESC LIMIT 1", new { });

            if (sub == null)
                return Ok(new { active = false, status = "none", message = "No subscription found" });

            var isActive = sub.Status == SubscriptionStatus.Active &&
                           (sub.ExpiresAt == null || sub.ExpiresAt > DateTime.UtcNow);

            return Ok(new
            {
                active = isActive,
                status = sub.Status,
                planName = sub.PlanName,
                expiresAt = sub.ExpiresAt,
                email = sub.Email
            });
        }

        // ────────────────────────────────────────────────────────
        // Public: Return Stripe checkout/config info for the frontend
        // ────────────────────────────────────────────────────────
        [AllowAnonymous]
        [EnableRateLimiting("public")]
        [HttpGet("config")]
        public IActionResult GetConfig()
        {
            return Ok(new
            {
                checkoutUrl = _stripe.PaymentLinkUrl
            });
        }

        // ────────────────────────────────────────────────────────
        // Public: Get available plans and pricing
        // ────────────────────────────────────────────────────────
        [AllowAnonymous]
        [EnableRateLimiting("public")]
        [HttpGet("plans")]
        public IActionResult GetPlans()
        {
            return Ok(SubscriptionPlans.GetAllPlans());
        }

        // ────────────────────────────────────────────────────────
        // Admin: Get full subscription details
        // ────────────────────────────────────────────────────────
        [Authorize(Roles = "super-admin,admin")]
        [EnableRateLimiting("authenticated")]
        [HttpGet("details")]
        public async Task<IActionResult> GetDetails()
        {
            var subs = await db.GetAll<AppSubscription>(
                @"SELECT * FROM mechanic_db.""Subscriptions""
                  ORDER BY ""UpdatedAt"" DESC", new { });
            return Ok(subs);
        }

        // ────────────────────────────────────────────────────────
        // Admin: Manually activate subscription (for testing / override)
        // ────────────────────────────────────────────────────────
        [Authorize(Roles = "super-admin")]
        [EnableRateLimiting("authenticated")]
        [HttpPost("activate")]
        public async Task<IActionResult> ManualActivate([FromBody] ManualActivateRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.PlanName) || req.ExpiresAt == null)
                return BadRequest(new { message = ErrorMessages.RequiredFieldsMissing });
            var existing = await db.GetAsync<AppSubscription>(
                @"SELECT * FROM mechanic_db.""Subscriptions""
                  ORDER BY ""Id"" DESC LIMIT 1", new { });

            if (existing != null)
            {
                await db.EditData(
                    @"UPDATE mechanic_db.""Subscriptions"" SET
                      ""Status""='active',
                      ""PlanName""=@PlanName,
                      ""ExpiresAt""=@ExpiresAt,
                      ""UpdatedAt""=CURRENT_TIMESTAMP
                      WHERE ""Id""=@Id",
                    new { PlanName = req.PlanName ?? "Manual", ExpiresAt = req.ExpiresAt, Id = existing.Id });
            }
            else
            {
                await db.EditData(
                    @"INSERT INTO mechanic_db.""Subscriptions""
                      (""Email"", ""Status"", ""PlanName"", ""ExpiresAt"", ""StartDate"")
                      VALUES (@Email, 'active', @PlanName, @ExpiresAt, CURRENT_TIMESTAMP)",
                    new { Email = req.Email ?? "admin@mechanicapp.local", PlanName = req.PlanName ?? "Manual", ExpiresAt = req.ExpiresAt });
            }

            return Ok(new { message = "Subscription activated" });
        }

        // ────────────────────────────────────────────────────────
        // Stripe Webhook — receives payment notifications
        // Docs: https://docs.stripe.com/webhooks
        // ────────────────────────────────────────────────────────
        [AllowAnonymous]
        [EnableRateLimiting("webhook")]
        [HttpPost("webhook/stripe")]
        public async Task<IActionResult> StripeWebhook()
        {
            // Read raw body
            using var reader = new StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync();

            // Verify Stripe webhook signature (HMAC-SHA256) when a secret is configured.
            // An attacker cannot forge this without the webhook secret.
            if (!string.IsNullOrEmpty(_stripe.WebhookSecret))
            {
                var signature = Request.Headers["Stripe-Signature"].FirstOrDefault() ?? string.Empty;
                try
                {
                    // ConstructEvent validates the HMAC-SHA256 signature and throws StripeException on failure.
                    EventUtility.ConstructEvent(body, signature, _stripe.WebhookSecret);
                }
                catch (StripeException ex)
                {
                    logger.LogWarning(ex, "Stripe webhook signature verification failed from {IP}", HttpContext.Connection.RemoteIpAddress);
                    return Unauthorized(new { message = "Invalid Stripe-Signature header" });
                }
            }

            // Parse the webhook payload
            JsonDocument? doc;
            try
            {
                doc = JsonDocument.Parse(body);
            }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "Invalid Stripe webhook JSON from {IP}", HttpContext.Connection.RemoteIpAddress);
                return BadRequest(new { message = "Invalid JSON body" });
            }

            var root = doc.RootElement;

            // Extract Stripe event type and data
            var eventType = root.TryGetProperty("type", out var t) ? t.GetString() : null;
            var customerEmail = "";
            var sessionId = "";
            var subscriptionId = "";

            if (root.TryGetProperty("data", out var data) &&
                data.TryGetProperty("object", out var obj))
            {
                if (obj.TryGetProperty("customer_email", out var email))
                    customerEmail = email.GetString() ?? "";
                else if (obj.TryGetProperty("customer_details", out var details) &&
                         details.TryGetProperty("email", out var detailEmail))
                    customerEmail = detailEmail.GetString() ?? "";

                if (obj.TryGetProperty("id", out var id))
                    sessionId = id.GetString() ?? "";

                if (obj.TryGetProperty("subscription", out var sub))
                    subscriptionId = sub.GetString() ?? "";
            }

            // Map Stripe event types to subscription statuses
            var status = eventType switch
            {
                "checkout.session.completed" => SubscriptionStatus.Active,
                "invoice.paid" => SubscriptionStatus.Active,
                "customer.subscription.created" => SubscriptionStatus.Active,
                "customer.subscription.updated" => SubscriptionStatus.Active,
                "customer.subscription.deleted" => SubscriptionStatus.Cancelled,
                "invoice.payment_failed" => SubscriptionStatus.Inactive,
                "charge.refunded" => SubscriptionStatus.Refunded,
                "charge.dispute.created" => SubscriptionStatus.Refunded,
                _ => (string?)null
            };

            if (status == null)
                return Ok(new { message = $"Event '{eventType}' acknowledged but no action taken" });

            // Upsert subscription
            var existing = await db.GetAsync<AppSubscription>(
                @"SELECT * FROM mechanic_db.""Subscriptions""
                  WHERE ""StripeSessionId"" = @SessionId
                     OR ""StripeSubscriptionId"" = @SubscriptionId
                     OR ""Email"" = @Email
                  ORDER BY ""Id"" DESC LIMIT 1",
                new { SessionId = sessionId, SubscriptionId = subscriptionId, Email = customerEmail });

            if (existing != null)
            {
                await db.EditData(
                    @"UPDATE mechanic_db.""Subscriptions"" SET
                      ""Status""=@Status,
                      ""StripeSessionId""=COALESCE(@SessionId, ""StripeSessionId""),
                      ""StripeSubscriptionId""=COALESCE(@SubscriptionId, ""StripeSubscriptionId""),
                      ""StripePayload""=@Payload::JSONB,
                      ""ExpiresAt""= CASE WHEN @Status='active' THEN CURRENT_TIMESTAMP + INTERVAL '30 days' ELSE ""ExpiresAt"" END,
                      ""UpdatedAt""=CURRENT_TIMESTAMP
                      WHERE ""Id""=@Id",
                    new
                    {
                        Status = status,
                        SessionId = string.IsNullOrEmpty(sessionId) ? null : sessionId,
                        SubscriptionId = string.IsNullOrEmpty(subscriptionId) ? null : subscriptionId,
                        Payload = body,
                        Id = existing.Id
                    });
            }
            else
            {
                await db.EditData(
                    @"INSERT INTO mechanic_db.""Subscriptions""
                      (""Email"", ""StripeSessionId"", ""StripeSubscriptionId"", ""Status"",
                       ""PlanName"", ""StartDate"", ""ExpiresAt"", ""StripePayload"")
                      VALUES (@Email, @SessionId, @SubscriptionId, @Status,
                              'Stripe', CURRENT_TIMESTAMP,
                              CASE WHEN @Status='active' THEN CURRENT_TIMESTAMP + INTERVAL '30 days' ELSE NULL END,
                              @Payload::JSONB)",
                    new
                    {
                        Email = customerEmail,
                        SessionId = string.IsNullOrEmpty(sessionId) ? null : sessionId,
                        SubscriptionId = string.IsNullOrEmpty(subscriptionId) ? null : subscriptionId,
                        Status = status,
                        Payload = body
                    });
            }

            return Ok(new { message = $"Webhook processed: {eventType} -> {status}" });
        }

        // ────────────────────────────────────────────────────────
        // POST: Self-service onboarding — new client pays → provision tenant
        // ────────────────────────────────────────────────────────
        [AllowAnonymous]
        [EnableRateLimiting("public")]
        [HttpPost("onboard")]
        public async Task<IActionResult> Onboard([FromBody] TenantOnboardRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.CompanyName))
                return BadRequest(new { message = "Email and CompanyName are required." });

            if (string.IsNullOrWhiteSpace(req.AdminPassword))
                return BadRequest(new { message = "AdminPassword is required." });

            var username = string.IsNullOrWhiteSpace(req.Username)
                ? "administrador"
                : req.Username.Trim().ToLowerInvariant();

            // Check if already has a tenant
            var existing = await tenantProvisioning.GetTenantByEmailAsync(req.Email);
            if (existing != null && !existing.IsDemo)
                return Conflict(new { message = "An account already exists for this email." });

            // If has a demo, convert it
            if (existing != null && existing.IsDemo)
            {
                var upgradePlan = req.PlanName ?? "standard";
                var converted = await tenantProvisioning.ConvertDemoToPaidAsync(existing.Id, upgradePlan, null);

                // Create active subscription with 30-day grace period
                await tenantProvisioning.CreateActiveSubscriptionAsync(
                    converted.DatabaseName, req.Email, upgradePlan);

                // Set admin credentials if provided
                if (!string.IsNullOrWhiteSpace(req.AdminPassword))
                {
                    await tenantProvisioning.SetAdminCredentialsAsync(
                        converted.DatabaseName, req.Email, req.AdminPassword);
                }

                // Update username if custom
                if (username != "administrador")
                {
                    await tenantProvisioning.SetAdminUsernameAsync(converted.DatabaseName, username);
                }

                var upgradePaymentUrl = BuildPaymentUrl(req.Email, upgradePlan);
                return Ok(new
                {
                    message = "Your demo has been upgraded to a paid plan. All data preserved!",
                    tenant = new
                    {
                        converted.Slug,
                        converted.PlanName,
                        converted.SubscriptionExpiresAt,
                        loginUrl = $"/{converted.Slug}/login"
                    },
                    credentials = new { username, password = "(the password you entered)" },
                    paymentUrl = upgradePaymentUrl
                });
            }

            // Provision new tenant
            var planName = req.PlanName ?? "standard";
            var tenant = await tenantProvisioning.ProvisionTenantAsync(
                req.CompanyName, req.Email, planName);

            // Set the admin credentials with the user-provided password
            if (!string.IsNullOrWhiteSpace(req.AdminPassword))
            {
                await tenantProvisioning.SetAdminCredentialsAsync(
                    tenant.DatabaseName, req.Email, req.AdminPassword);
            }

            // Update username if custom
            if (username != "administrador")
            {
                await tenantProvisioning.SetAdminUsernameAsync(tenant.DatabaseName, username);
            }

            // Create active subscription with 30-day grace period
            await tenantProvisioning.CreateActiveSubscriptionAsync(
                tenant.DatabaseName, req.Email, planName);

            // Build Stripe payment URL with email prefilled
            var paymentUrl = BuildPaymentUrl(req.Email, planName);

            return Ok(new
            {
                message = "Account created successfully!",
                tenant = new
                {
                    tenant.Slug,
                    tenant.PlanName,
                    tenant.SubscriptionExpiresAt,
                    loginUrl = $"/{tenant.Slug}/login"
                },
                credentials = new { username, password = "(the password you entered)" },
                paymentUrl
            });
        }

        // ────────────────────────────────────────────────────────
        // Helper: Build Stripe payment link URL with prefilled data
        // ────────────────────────────────────────────────────────
        private string BuildPaymentUrl(string email, string planName)
        {
            if (string.IsNullOrEmpty(_stripe.PaymentLinkUrl))
                return string.Empty;

            var separator = _stripe.PaymentLinkUrl.Contains('?') ? "&" : "?";
            return $"{_stripe.PaymentLinkUrl}{separator}prefilled_email={Uri.EscapeDataString(email)}";
        }
    }
}
