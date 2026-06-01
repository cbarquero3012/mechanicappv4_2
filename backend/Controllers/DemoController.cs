using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using MechanicApp.Server.Constants;
using MechanicApp.Server.Models;
using MechanicApp.Server.Options;
using MechanicApp.Server.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace MechanicApp.Server.Controllers
{
    /// <summary>
    /// Public-facing demo provisioning endpoint.
    /// Allows prospects to instantly try MechanicApp without signup.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [EnableRateLimiting("public")]
    public class DemoController(ITenantProvisioningService provisioning, IEmailService emailService, IOptions<SmtpSettings> smtpOptions) : ControllerBase
    {
        // ────────────────────────────────────────────────────────
        // POST: Create a new demo instance
        // ────────────────────────────────────────────────────────
        [HttpPost("create")]
        public async Task<IActionResult> CreateDemo([FromBody] CreateDemoRequest req)
        {
            var name = string.IsNullOrWhiteSpace(req.Name)
                ? $"demo_{DateTime.UtcNow:yyyyMMddHHmmss}"
                : req.Name;

            // Validate email format when explicitly provided
            if (!string.IsNullOrWhiteSpace(req.Email) && !new EmailAddressAttribute().IsValid(req.Email))
                return BadRequest(new { message = "Invalid email address format." });

            var email = string.IsNullOrWhiteSpace(req.Email)
                ? $"demo_{Guid.NewGuid():N}@demo.mechanicapp.com"
                : req.Email;

            var username = string.IsNullOrWhiteSpace(req.Username)
                ? "administrador"
                : req.Username.Trim().ToLowerInvariant();

            // Generate a random password for each demo
            var demoPassword = GenerateRandomPassword();

            // Check if this email already has a demo
            if (!string.IsNullOrWhiteSpace(req.Email))
            {
                var existing = await provisioning.GetTenantByEmailAsync(req.Email);
                if (existing != null && existing.IsDemo)
                {
                    return Ok(new
                    {
                        message = "You already have an active demo.",
                        tenant = new
                        {
                            existing.Slug,
                            existing.DemoExpiresAt,
                            existing.Status,
                            loginUrl = $"/{existing.Slug}/login",
                            credentials = new { username, password = "(use your original demo password)" }
                        }
                    });
                }
            }

            var tenant = await provisioning.ProvisionTenantAsync(name, email, "demo", isDemo: true);

            // Set custom credentials in the tenant database
            await provisioning.SetAdminCredentialsAsync(tenant.DatabaseName, email, demoPassword);

            // If a custom username was provided, update the username too
            if (username != "administrador")
            {
                await provisioning.SetAdminUsernameAsync(tenant.DatabaseName, username);
            }

            // Send welcome email
            var baseUrl = smtpOptions.Value.FrontendBaseUrl?.TrimEnd('/') ?? "";
            var loginUrl = $"{baseUrl}/{tenant.Slug}/login";
            await emailService.SendWelcomeEmailAsync(
                email, username, loginUrl, tenant.PlanName ?? "free-trial",
                password: demoPassword, expiresAt: tenant.DemoExpiresAt, isDemo: true);

            return Ok(new
            {
                message = "Demo created successfully! Your trial expires in 7 days.",
                tenant = new
                {
                    tenant.Slug,
                    tenant.DemoExpiresAt,
                    tenant.Status,
                    loginUrl = $"/{tenant.Slug}/login",
                    credentials = new { username, password = demoPassword }
                }
            });
        }

        private static string GenerateRandomPassword()
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789";
            var bytes = new byte[10];
            RandomNumberGenerator.Fill(bytes);
            return new string(bytes.Select(b => chars[b % chars.Length]).ToArray());
        }

        // ────────────────────────────────────────────────────────
        // GET: Check demo status
        // ────────────────────────────────────────────────────────
        [HttpGet("status/{slug}")]
        public async Task<IActionResult> GetDemoStatus(string slug)
        {
            var tenant = await provisioning.GetTenantBySlugAsync(slug);

            if (tenant == null || !tenant.IsDemo)
                return NotFound(new { message = "Demo not found" });

            var isExpired = tenant.DemoExpiresAt.HasValue && tenant.DemoExpiresAt < DateTime.UtcNow;

            return Ok(new
            {
                tenant.Slug,
                tenant.Name,
                tenant.DemoExpiresAt,
                isExpired,
                daysRemaining = isExpired ? 0 : (int)Math.Ceiling((tenant.DemoExpiresAt!.Value - DateTime.UtcNow).TotalDays)
            });
        }

        // ────────────────────────────────────────────────────────
        // POST: Onboard from demo → paid (self-service upgrade)
        // ────────────────────────────────────────────────────────
        [HttpPost("upgrade")]
        public async Task<IActionResult> UpgradeDemo([FromBody] TenantOnboardRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.CompanyName))
                return BadRequest(new { message = "Email and CompanyName are required." });

            var tenant = await provisioning.GetTenantByEmailAsync(req.Email);
            if (tenant == null)
                return NotFound(new { message = "No demo found for this email." });

            if (!tenant.IsDemo)
                return BadRequest(new { message = "This tenant is already a paid account." });

            var upgraded = await provisioning.ConvertDemoToPaidAsync(
                tenant.Id, req.PlanName ?? SubscriptionPlans.Standard, null);

            return Ok(new
            {
                message = "Demo upgraded to paid plan! Your data has been preserved.",
                tenant = new
                {
                    upgraded.Slug,
                    upgraded.Status,
                    upgraded.PlanName,
                    upgraded.SubscriptionExpiresAt
                }
            });
        }
    }
}
