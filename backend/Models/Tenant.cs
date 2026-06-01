namespace MechanicApp.Server.Models
{
    /// <summary>
    /// Represents a SaaS tenant with its own isolated PostgreSQL database.
    /// Each tenant maps to one workshop / company using MechanicApp.
    /// </summary>
    public class Tenant
    {
        /// <summary>Auto-generated primary key.</summary>
        public int Id { get; set; }

        /// <summary>Human-readable company or workshop name.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>URL-safe slug used as a subdomain / path prefix (e.g., "my-workshop").</summary>
        public string Slug { get; set; } = string.Empty;

        /// <summary>Primary contact email used for billing and notifications.</summary>
        public string Email { get; set; } = string.Empty;

        /// <summary>Tenant lifecycle status. See <see cref="TenantStatus"/> constants.</summary>
        public string Status { get; set; } = "active";

        /// <summary>Name of the isolated PostgreSQL database for this tenant.</summary>
        public string DatabaseName { get; set; } = string.Empty;

        /// <summary>Stripe customer ID, set after the first payment interaction. Nullable until Stripe is linked.</summary>
        public string? StripeCustomerId { get; set; }

        /// <summary>Active Stripe subscription ID. Null for trial or manually-managed plans.</summary>
        public string? StripeSubscriptionId { get; set; }

        /// <summary>Active subscription plan name (e.g., "trial", "standard", "premium").</summary>
        public string PlanName { get; set; } = "trial";

        /// <summary>Maximum number of user accounts allowed under this plan.</summary>
        public int MaxUsers { get; set; } = 5;

        /// <summary>True if this tenant was provisioned as a limited-time demo instance.</summary>
        public bool IsDemo { get; set; }

        /// <summary>
        /// When the demo trial expires. Null for paid tenants.
        /// A background job deactivates and cleans up tenants past this date.
        /// </summary>
        public DateTime? DemoExpiresAt { get; set; }

        /// <summary>When the current paid subscription expires. Null while in trial or if managed externally via Stripe webhooks.</summary>
        public DateTime? SubscriptionExpiresAt { get; set; }

        /// <summary>ISO 3166-1 alpha-2 country code for the tenant's locale (e.g., "CR", "US"). Optional.</summary>
        public string? Country { get; set; }

        /// <summary>UTC timestamp of when the tenant was provisioned.</summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>UTC timestamp of the last update to this tenant record.</summary>
        public DateTime? UpdatedAt { get; set; }
    }
}
