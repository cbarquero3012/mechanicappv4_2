using MechanicApp.Server.Constants;
using MechanicApp.Server.Models;

namespace MechanicApp.Server.Services
{
    /// <summary>
    /// Provides aggregated dashboard statistics and KPIs.
    /// </summary>
    public class DashboardService(IDbService db) : IDashboardService
    {
        /// <inheritdoc />
        public async Task<DashboardStatsResult> GetStatsAsync(bool isMechanic, int? mechanicId)
        {
            var filterParam = new { MechanicId = mechanicId ?? 0 };
            string mechanicFilter = isMechanic ? @" WHERE r.""MechanicId"" = @MechanicId" : "";
            string mechanicAndFilter = isMechanic ? @" AND r.""MechanicId"" = @MechanicId" : "";

            var defaultCurrency = await db.GetAsync<CurrencySymbolResult>(
                @"SELECT ""Symbol"" FROM mechanic_db.""Currencies"" WHERE ""IsDefault"" = TRUE LIMIT 1", new { }).ConfigureAwait(false);

            var customerCount = await db.GetAsync<CountResult>(
                @"SELECT COUNT(*) AS ""Value"" FROM mechanic_db.""Customers""", new { }).ConfigureAwait(false);
            var vehicleCount = await db.GetAsync<CountResult>(
                @"SELECT COUNT(*) AS ""Value"" FROM mechanic_db.""DetailsCars""", new { }).ConfigureAwait(false);
            var mechanicCount = await db.GetAsync<CountResult>(
                @"SELECT COUNT(*) AS ""Value"" FROM mechanic_db.""Mechanics""", new { }).ConfigureAwait(false);

            var totalOrders = await db.GetAsync<CountResult>(
                @"SELECT COUNT(*) AS ""Value"" FROM mechanic_db.""RepairOrders"" r" + mechanicFilter, filterParam).ConfigureAwait(false);
            var pendingOrders = await db.GetAsync<CountResult>(
                @"SELECT COUNT(*) AS ""Value"" FROM mechanic_db.""RepairOrders"" r WHERE r.""Status"" = @Status" + mechanicAndFilter,
                new { MechanicId = mechanicId ?? 0, Status = OrderStatus.Pending }).ConfigureAwait(false);
            var inProgressOrders = await db.GetAsync<CountResult>(
                @"SELECT COUNT(*) AS ""Value"" FROM mechanic_db.""RepairOrders"" r WHERE r.""Status"" = @Status" + mechanicAndFilter,
                new { MechanicId = mechanicId ?? 0, Status = OrderStatus.InProgress }).ConfigureAwait(false);
            var completedOrders = await db.GetAsync<CountResult>(
                @"SELECT COUNT(*) AS ""Value"" FROM mechanic_db.""RepairOrders"" r WHERE r.""Status"" = @Status" + mechanicAndFilter,
                new { MechanicId = mechanicId ?? 0, Status = OrderStatus.Completed }).ConfigureAwait(false);

            var totalRevenue = await db.GetAsync<DecimalResult>(
                @"SELECT COALESCE(SUM(r.""TotalCost""), 0) AS ""Value"" FROM mechanic_db.""RepairOrders"" r" + mechanicFilter, filterParam).ConfigureAwait(false);
            var paidRevenue = await db.GetAsync<DecimalResult>(
                @"SELECT COALESCE(SUM(p.""Amount""), 0) AS ""Value"" FROM mechanic_db.""Payments"" p" +
                (isMechanic ? @" WHERE p.""Id"" IN (SELECT pro.""PaymentId"" FROM mechanic_db.""PaymentRepairOrders"" pro JOIN mechanic_db.""RepairOrders"" r ON pro.""RepairOrderId"" = r.""Id"" WHERE r.""MechanicId"" = @MechanicId)" : ""), filterParam).ConfigureAwait(false);

            var recentOrders = await db.GetAll<RecentOrderDto>(
                @"SELECT r.""Id"", r.""Status"", r.""TotalCost"", r.""OrderDate"",
                    b.""BrandName"" || ' ' || m.""ModelName"" || ' (' || d.""Year"" || ')' AS ""CarInfo"",
                    me.""FirstName"" || ' ' || me.""LastName"" AS ""MechanicName""
                  FROM mechanic_db.""RepairOrders"" r
                  LEFT JOIN mechanic_db.""DetailsCars"" d ON r.""DetailCarId"" = d.""Id""
                  LEFT JOIN mechanic_db.""CarModels"" m ON d.""CarModelId"" = m.""Id""
                  LEFT JOIN mechanic_db.""CarBrands"" b ON m.""BrandId"" = b.""Id""
                  LEFT JOIN mechanic_db.""Mechanics"" me ON r.""MechanicId"" = me.""Id""" +
                  mechanicFilter +
                  @" ORDER BY r.""CreatedAt"" DESC LIMIT 5", filterParam).ConfigureAwait(false);

            return new DashboardStatsResult
            {
                CustomerCount = customerCount?.Value ?? 0,
                VehicleCount = vehicleCount?.Value ?? 0,
                MechanicCount = mechanicCount?.Value ?? 0,
                TotalOrders = totalOrders?.Value ?? 0,
                PendingOrders = pendingOrders?.Value ?? 0,
                InProgressOrders = inProgressOrders?.Value ?? 0,
                CompletedOrders = completedOrders?.Value ?? 0,
                TotalRevenue = totalRevenue?.Value ?? 0m,
                PaidRevenue = paidRevenue?.Value ?? 0m,
                RecentOrders = recentOrders,
                CurrencySymbol = defaultCurrency?.Symbol ?? "₡"
            };
        }
    }
}
