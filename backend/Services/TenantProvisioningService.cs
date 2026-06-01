using System.Text.RegularExpressions;
using Dapper;
using MechanicApp.Server.Constants;
using MechanicApp.Server.Models;
using Npgsql;

namespace MechanicApp.Server.Services
{
    public class TenantProvisioningService(
        IConfiguration configuration,
        ILogger<TenantProvisioningService> logger) : ITenantProvisioningService
    {
        private readonly string _controlConnString = configuration.GetConnectionString("ControlPlane")!;
        private readonly string _adminConnString = configuration.GetConnectionString("AdminConnection")!;

        public async Task<Tenant> ProvisionTenantAsync(string name, string email, string planName, bool isDemo = false, string? country = null)
        {
            var slug = GenerateSlug(name);
            var dbPrefix = isDemo ? "mechanic_app_demo_tenant" : "mechanic_app_tenant";
            var dbName = $"{dbPrefix}_{slug}_{DateTime.UtcNow:yyyyMMdd}";

            logger.LogInformation("Provisioning tenant database: {DbName} for {Email}", dbName, email);

            // 1. Create the tenant database from template
            await CreateDatabaseFromTemplateAsync(dbName).ConfigureAwait(false);

            // 2. Register in control plane
            var tenant = new Tenant
            {
                Name = name,
                Slug = slug,
                Email = email,
                Status = isDemo ? TenantStatus.Demo : TenantStatus.Active,
                DatabaseName = dbName,
                PlanName = isDemo ? SubscriptionPlans.FreeTrial : planName,
                IsDemo = isDemo,
                DemoExpiresAt = isDemo ? DateTime.UtcNow.AddDays(SubscriptionPlans.GetTrialDays(SubscriptionPlans.FreeTrial)) : null,
                SubscriptionExpiresAt = isDemo ? null : DateTime.UtcNow.AddDays(30),
                MaxUsers = SubscriptionPlans.GetMaxUsers(isDemo ? SubscriptionPlans.FreeTrial : planName),
                CreatedAt = DateTime.UtcNow
            };

            await using var conn = new NpgsqlConnection(_controlConnString);
            var id = await conn.QuerySingleAsync<int>(
                @"INSERT INTO control_plane.""Tenants""
                  (""Name"", ""Slug"", ""Email"", ""Status"", ""DatabaseName"",
                   ""PlanName"", ""MaxUsers"", ""IsDemo"", ""DemoExpiresAt"",
                   ""SubscriptionExpiresAt"", ""CreatedAt"")
                  VALUES (@Name, @Slug, @Email, @Status, @DatabaseName,
                          @PlanName, @MaxUsers, @IsDemo, @DemoExpiresAt,
                          @SubscriptionExpiresAt, @CreatedAt)
                  RETURNING ""Id""",
                tenant).ConfigureAwait(false);

            tenant.Id = id;

            // 3. Seed demo data if it's a demo tenant
            if (isDemo)
            {
                await SeedDemoDataAsync(dbName).ConfigureAwait(false);
            }

            logger.LogInformation("Tenant provisioned successfully: {TenantId} ({DbName})", id, dbName);
            return tenant;
        }

        public async Task<Tenant> ConvertDemoToPaidAsync(int tenantId, string planName, string? stripeSubscriptionId)
        {
            await using var conn = new NpgsqlConnection(_controlConnString);

            await conn.ExecuteAsync(
                @"UPDATE control_plane.""Tenants"" SET
                  ""Status"" = @Status,
                  ""IsDemo"" = FALSE,
                  ""PlanName"" = @PlanName,
                  ""StripeSubscriptionId"" = @StripeSubscriptionId,
                  ""SubscriptionExpiresAt"" = @ExpiresAt,
                  ""DemoExpiresAt"" = NULL,
                  ""MaxUsers"" = @MaxUsers,
                  ""UpdatedAt"" = CURRENT_TIMESTAMP
                  WHERE ""Id"" = @Id",
                new
                {
                    Status = TenantStatus.Active,
                    PlanName = planName,
                    StripeSubscriptionId = stripeSubscriptionId,
                    ExpiresAt = DateTime.UtcNow.AddDays(30),
                    MaxUsers = SubscriptionPlans.GetMaxUsers(planName),
                    Id = tenantId
                }).ConfigureAwait(false);

            return (await GetTenantByIdAsync(tenantId).ConfigureAwait(false))!;
        }

        public async Task<int> CleanupExpiredDemosAsync()
        {
            await using var conn = new NpgsqlConnection(_controlConnString);

            var expired = await conn.QueryAsync<Tenant>(
                @"SELECT * FROM control_plane.""Tenants""
                  WHERE ""IsDemo"" = TRUE AND ""DemoExpiresAt"" < @Now",
                new { Now = DateTime.UtcNow }).ConfigureAwait(false);

            var count = 0;
            foreach (var tenant in expired)
            {
                try
                {
                    await DropDatabaseAsync(tenant.DatabaseName).ConfigureAwait(false);
                    await conn.ExecuteAsync(
                        @"DELETE FROM control_plane.""Tenants"" WHERE ""Id"" = @Id",
                        new { tenant.Id }).ConfigureAwait(false);
                    count++;
                    logger.LogInformation("Cleaned up expired demo: {DbName}", tenant.DatabaseName);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to cleanup demo tenant {Id}: {DbName}", tenant.Id, tenant.DatabaseName);
                }
            }

            return count;
        }

        public async Task<Tenant?> GetTenantBySlugAsync(string slug)
        {
            await using var conn = new NpgsqlConnection(_controlConnString);
            return await conn.QueryFirstOrDefaultAsync<Tenant>(
                @"SELECT * FROM control_plane.""Tenants"" WHERE ""Slug"" = @Slug",
                new { Slug = slug }).ConfigureAwait(false);
        }

        public async Task<Tenant?> GetTenantByEmailAsync(string email)
        {
            await using var conn = new NpgsqlConnection(_controlConnString);
            return await conn.QueryFirstOrDefaultAsync<Tenant>(
                @"SELECT * FROM control_plane.""Tenants"" WHERE ""Email"" = @Email ORDER BY ""Id"" DESC LIMIT 1",
                new { Email = email }).ConfigureAwait(false);
        }

        public async Task<List<Tenant>> GetAllTenantsAsync()
        {
            await using var conn = new NpgsqlConnection(_controlConnString);
            var result = await conn.QueryAsync<Tenant>(
                @"SELECT * FROM control_plane.""Tenants"" ORDER BY ""CreatedAt"" DESC").ConfigureAwait(false);
            return result.ToList();
        }

        public async Task SeedDemoDataAsync(string databaseName)
        {
            var connString = BuildTenantConnectionString(databaseName);
            await using var conn = new NpgsqlConnection(connString);
            await conn.OpenAsync().ConfigureAwait(false);

            // Seed sample customers
            await conn.ExecuteAsync(
                @"INSERT INTO mechanic_db.""Customers"" (""FirstName"", ""LastName"", ""Email"", ""PhoneNumber"", ""Address"")
                  VALUES
                  ('Carlos', 'Demo', 'carlos@demo.com', '8888-1111', '123 Demo Street'),
                  ('María', 'Demo', 'maria@demo.com', '8888-2222', '456 Sample Ave'),
                  ('Luis', 'Demo', 'luis@demo.com', '8888-3333', '789 Test Blvd')
                  ON CONFLICT DO NOTHING").ConfigureAwait(false);

            // Seed sample mechanic
            await conn.ExecuteAsync(
                @"INSERT INTO mechanic_db.""Mechanics"" (""FirstName"", ""LastName"", ""Specialty"", ""HireDate"", ""IsActive"")
                  VALUES ('Juan', 'Demo Mechanic', 'General', CURRENT_DATE, TRUE)
                  ON CONFLICT DO NOTHING").ConfigureAwait(false);

            logger.LogInformation("Demo data seeded for database: {DbName}", databaseName);
        }

        /// <summary>
        /// Sets (or resets) the admin user's password and email in a tenant database.
        /// Used after onboarding to personalize the template-cloned admin account.
        /// </summary>
        public async Task SetAdminCredentialsAsync(string databaseName, string email, string password)
        {
            var connString = BuildTenantConnectionString(databaseName);
            await using var conn = new NpgsqlConnection(connString);
            await conn.OpenAsync().ConfigureAwait(false);

            var passwordHash = BCrypt.Net.BCrypt.HashPassword(password);

            await conn.ExecuteAsync(
                @"UPDATE mechanic_db.""Users""
                  SET ""PasswordHash"" = @PasswordHash, ""Email"" = @Email
                  WHERE ""Role"" = 'admin' AND ""Username"" = 'administrador'",
                new { PasswordHash = passwordHash, Email = email }).ConfigureAwait(false);

            logger.LogInformation("Admin credentials set for database: {DbName}", databaseName);
        }

        public async Task SetAdminUsernameAsync(string databaseName, string username)
        {
            var connString = BuildTenantConnectionString(databaseName);
            await using var conn = new NpgsqlConnection(connString);
            await conn.OpenAsync().ConfigureAwait(false);

            await conn.ExecuteAsync(
                @"UPDATE mechanic_db.""Users""
                  SET ""Username"" = @Username
                  WHERE ""Role"" = 'admin' AND ""Username"" = 'administrador'",
                new { Username = username }).ConfigureAwait(false);

            logger.LogInformation("Admin username updated to '{Username}' for database: {DbName}", username, databaseName);
        }

        public async Task CreatePendingSubscriptionAsync(string databaseName, string email, string planName)
        {
            await CreateActiveSubscriptionAsync(databaseName, email, planName).ConfigureAwait(false);
        }

        public async Task CreateActiveSubscriptionAsync(string databaseName, string email, string planName)
        {
            var connString = BuildTenantConnectionString(databaseName);
            await using var conn = new NpgsqlConnection(connString);
            await conn.OpenAsync().ConfigureAwait(false);

            await conn.ExecuteAsync(
                @"INSERT INTO mechanic_db.""Subscriptions""
                  (""Email"", ""Status"", ""PlanName"", ""StartDate"", ""ExpiresAt"")
                  VALUES (@Email, 'active', @PlanName, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP + INTERVAL '30 days')
                  ON CONFLICT DO NOTHING",
                new { Email = email, PlanName = planName }).ConfigureAwait(false);

            logger.LogInformation("Active subscription created for database: {DbName}, plan: {Plan}", databaseName, planName);
        }

        // ──────── Private Helpers ────────

        private async Task CreateDatabaseFromTemplateAsync(string dbName)
        {
            // Connect to 'postgres' system DB to issue CREATE DATABASE
            await using var conn = new NpgsqlConnection(_adminConnString);
            await conn.OpenAsync().ConfigureAwait(false);

            // Terminate existing connections to template (required for TEMPLATE cloning)
            await conn.ExecuteAsync(
                @"SELECT pg_terminate_backend(pid)
                  FROM pg_stat_activity
                  WHERE datname = 'mechanic_template' AND pid <> pg_backend_pid()").ConfigureAwait(false);

            // CREATE DATABASE cannot be parameterized, but we sanitize the name
            var safeName = SanitizeDbName(dbName);
            await conn.ExecuteAsync($"CREATE DATABASE \"{safeName}\" TEMPLATE mechanic_template").ConfigureAwait(false);
        }

        private async Task DropDatabaseAsync(string dbName)
        {
            await using var conn = new NpgsqlConnection(_adminConnString);
            await conn.OpenAsync().ConfigureAwait(false);

            var safeName = SanitizeDbName(dbName);

            // Terminate all connections first — DQL query uses a parameter, DDL (DROP) uses quoted identifier
            await conn.ExecuteAsync(
                "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = @DbName AND pid <> pg_backend_pid()",
                new { DbName = safeName }).ConfigureAwait(false);

            await conn.ExecuteAsync($"DROP DATABASE IF EXISTS \"{safeName}\"").ConfigureAwait(false);
        }

        private async Task<Tenant?> GetTenantByIdAsync(int id)
        {
            await using var conn = new NpgsqlConnection(_controlConnString);
            return await conn.QueryFirstOrDefaultAsync<Tenant>(
                @"SELECT * FROM control_plane.""Tenants"" WHERE ""Id"" = @Id",
                new { Id = id }).ConfigureAwait(false);
        }

        private string BuildTenantConnectionString(string databaseName)
        {
            var builder = new NpgsqlConnectionStringBuilder(_controlConnString)
            {
                Database = databaseName
            };
            return builder.ConnectionString;
        }

        private static string GenerateSlug(string name)
        {
            var slug = name.ToLowerInvariant().Trim();
            slug = Regex.Replace(slug, @"[^a-z0-9\s-]", "");
            slug = Regex.Replace(slug, @"[\s-]+", "_");
            slug = slug.Trim('_');
            return slug.Length > 30 ? slug[..30] : slug;
        }

        private static string SanitizeDbName(string name)
        {
            // Only allow alphanumeric and underscores to prevent SQL injection
            return Regex.Replace(name, @"[^a-zA-Z0-9_]", "");
        }
    }
}
