using MechanicApp.Server.Models;

namespace MechanicApp.Server.Services
{
    public interface ITenantProvisioningService
    {
        /// <summary>
        /// Creates a new tenant database by cloning the template and registers it in the control plane.
        /// </summary>
        Task<Tenant> ProvisionTenantAsync(string name, string email, string planName, bool isDemo = false, string? country = null);

        /// <summary>
        /// Converts a demo tenant to a paid tenant (preserves data).
        /// </summary>
        Task<Tenant> ConvertDemoToPaidAsync(int tenantId, string planName, string? stripeSubscriptionId);

        /// <summary>
        /// Drops expired demo databases and removes their tenant records.
        /// </summary>
        Task<int> CleanupExpiredDemosAsync();

        /// <summary>
        /// Gets a tenant by its slug (subdomain identifier).
        /// </summary>
        Task<Tenant?> GetTenantBySlugAsync(string slug);

        /// <summary>
        /// Gets a tenant by email address.
        /// </summary>
        Task<Tenant?> GetTenantByEmailAsync(string email);

        /// <summary>
        /// Gets all tenants.
        /// </summary>
        Task<List<Tenant>> GetAllTenantsAsync();

        /// <summary>
        /// Seeds a newly provisioned tenant database with demo data.
        /// </summary>
        Task SeedDemoDataAsync(string databaseName);

        /// <summary>
        /// Sets the admin user's password and email in a tenant database.
        /// </summary>
        Task SetAdminCredentialsAsync(string databaseName, string email, string password);

        /// <summary>
        /// Creates a pending subscription record in a tenant database so the subscription guard
        /// recognizes a valid (pending payment) state.
        /// </summary>
        Task CreatePendingSubscriptionAsync(string databaseName, string email, string planName);

        /// <summary>
        /// Creates an active subscription with 30-day grace period in a tenant database.
        /// Stripe webhook will extend or cancel when payment is confirmed.
        /// </summary>
        Task CreateActiveSubscriptionAsync(string databaseName, string email, string planName);

        /// <summary>
        /// Updates the admin user's username in a tenant database.
        /// </summary>
        Task SetAdminUsernameAsync(string databaseName, string username);
    }
}
