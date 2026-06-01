using MechanicApp.Server.Models;

namespace MechanicApp.Server.Services
{
    /// <summary>
    /// Provides aggregated dashboard statistics and KPIs.
    /// </summary>
    public interface IDashboardService
    {
        /// <summary>
        /// Retrieves dashboard statistics filtered by role context.
        /// </summary>
        /// <param name="isMechanic">Whether the requesting user is a mechanic.</param>
        /// <param name="mechanicId">The mechanic ID for scoped queries (null for admin).</param>
        Task<DashboardStatsResult> GetStatsAsync(bool isMechanic, int? mechanicId);
    }
}
