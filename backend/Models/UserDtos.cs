using System.ComponentModel.DataAnnotations;

namespace MechanicApp.Server.Models
{
    public class UserDto
    {
        public int Id { get; set; }
        public string Username { get; set; } = "";
        public string FullName { get; set; } = "";
        public string Email { get; set; } = "";
        public string Role { get; set; } = "mechanic";
        public bool Active { get; set; } = true;
        public int? MechanicId { get; set; }
        public string? Country { get; set; }
        public DateTime? CreatedAt { get; set; }
    }

    public class CreateUserRequest
    {
        [Required(ErrorMessage = "The Username field is required, please fill the field to continue!")]
        public string Username { get; set; } = "";

        [Required(ErrorMessage = "The Password field is required, please fill the field to continue!")]
        public string Password { get; set; } = "";

        [Required(ErrorMessage = "The Full Name field is required, please fill the field to continue!")]
        public string? FullName { get; set; }

        public string? Email { get; set; }

        [Required(ErrorMessage = "The Role field is required, please fill the field to continue!")]
        public string? Role { get; set; }

        public bool? Active { get; set; }
        public int? MechanicId { get; set; }
        public string? Country { get; set; }
    }

    public class UpdateUserRequest
    {
        public string? Username { get; set; }
        public string? Password { get; set; }
        public string? FullName { get; set; }
        public string? Email { get; set; }
        public string? Role { get; set; }
        public bool? Active { get; set; }
        public int? MechanicId { get; set; }
        public string? Country { get; set; }
    }
}
