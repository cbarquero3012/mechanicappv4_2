using System.ComponentModel.DataAnnotations;

namespace MechanicApp.Server.Models
{
    public class AppUser
    {
        public int Id { get; set; }

        [Required, StringLength(100)]
        public string Username { get; set; } = string.Empty;

        public string PasswordHash { get; set; } = string.Empty;

        [Required, StringLength(200)]
        public string FullName { get; set; } = string.Empty;

        [Required, EmailAddress, StringLength(200)]
        public string Email { get; set; } = string.Empty;

        [Required, StringLength(30)]
        public string Role { get; set; } = "mechanic";

        public bool Active { get; set; } = true;
        public int? MechanicId { get; set; }

        [StringLength(100)]
        public string? Country { get; set; }

        public DateTime? CreatedAt { get; set; }
    }
}
