using System.Security.Claims;
using MechanicApp.Server.Constants;
using MechanicApp.Server.Models;
using MechanicApp.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace MechanicApp.Server.Controllers
{
    /// <summary>
    /// Handles user authentication: login and current-user info.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController(IDbService db, ITokenService tokenService) : ControllerBase
    {
        /// <summary>
        /// Authenticates a user and returns a signed JWT.
        /// </summary>
        /// <param name="request">The login credentials.</param>
        [AllowAnonymous]
        [EnableRateLimiting("login")]
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
                return BadRequest(new { message = ErrorMessages.UsernamePasswordRequired });

            var user = await db.GetAsync<AppUser>(
                @"SELECT ""Id"", ""Username"", ""PasswordHash"", ""FullName"", ""Email"", ""Role"", ""Active"", ""MechanicId""
                  FROM mechanic_db.""Users""
                  WHERE ""Username"" = @Username",
                new { request.Username });

            if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
                return Unauthorized(new { message = ErrorMessages.InvalidCredentials });

            if (!user.Active)
                return Unauthorized(new { message = ErrorMessages.AccountDisabled });

            var token = tokenService.GenerateToken(user);
            var expiration = DateTime.UtcNow.AddMinutes(tokenService.ExpiresInMinutes);

            return Ok(new LoginResponse
            {
                Token = token,
                Username = user.Username,
                Expiration = expiration
            });
        }

        /// <summary>
        /// Returns the authenticated user's username and role.
        /// </summary>
        [Authorize]
        [EnableRateLimiting("authenticated")]
        [HttpGet("me")]
        public IActionResult GetCurrentUser()
        {
            var username = User.FindFirst(ClaimTypes.Name)?.Value;
            var role = User.FindFirst(ClaimTypes.Role)?.Value;
            return Ok(new { username, role });
        }
    }
}
