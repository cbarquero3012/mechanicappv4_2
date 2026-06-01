using System.Security.Claims;
using MechanicApp.Server.Constants;
using MechanicApp.Server.Extensions;
using MechanicApp.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace MechanicApp.Server.Controllers
{
    /// <summary>
    /// Provides aggregated dashboard statistics and KPIs.
    /// </summary>
    [Authorize]
    [EnableRateLimiting("authenticated")]
    [ApiController]
    [Route("api/[controller]")]
    public class DashboardController(IDashboardService dashboardService) : ControllerBase
    {
        [HttpGet("stats")]
        public async Task<IActionResult> GetStats()
        {
            var isMechanic = User.IsMechanicUser();
            var mechanicId = User.GetMechanicId();

            var stats = await dashboardService.GetStatsAsync(isMechanic, mechanicId);
            return Ok(stats);
        }
    }
}
