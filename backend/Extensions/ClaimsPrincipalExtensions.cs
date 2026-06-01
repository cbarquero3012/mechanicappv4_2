using System.Security.Claims;
using MechanicApp.Server.Constants;

namespace MechanicApp.Server.Extensions
{
    /// <summary>
    /// Extension methods for ClaimsPrincipal to simplify claim extraction.
    /// </summary>
    public static class ClaimsPrincipalExtensions
    {
        /// <summary>Gets the user's role from claims.</summary>
        public static string GetRole(this ClaimsPrincipal user)
            => user.FindFirst(ClaimTypes.Role)?.Value ?? "";

        /// <summary>Gets the user's mechanic ID from claims, if present.</summary>
        public static int? GetMechanicId(this ClaimsPrincipal user)
        {
            var claim = user.FindFirst("mechanicId")?.Value;
            return int.TryParse(claim, out var id) ? id : null;
        }

        /// <summary>Returns true if the user has a mechanic role with a valid mechanic ID.</summary>
        public static bool IsMechanicUser(this ClaimsPrincipal user)
            => user.GetRole() == AppRoles.Mechanic && user.GetMechanicId().HasValue;
    }
}
