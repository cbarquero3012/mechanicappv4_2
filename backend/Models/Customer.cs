using System.ComponentModel.DataAnnotations;

namespace MechanicApp.Server.Models
{
    public class Customer
    {
        public int Id { get; set; }

        [Required, StringLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required, StringLength(100)]
        public string LastName { get; set; } = string.Empty;

        [EmailAddress, StringLength(200)]
        public string? Email { get; set; }

        [Required, StringLength(20)]
        public string PhoneNumber { get; set; } = string.Empty;

        [StringLength(300)]
        public string? Address { get; set; }

        /// <summary>Client identifier (e.g. national ID, tax number).</summary>
        [Required(ErrorMessage = "The Client ID field is required, please fill the field to continue!")]
        [StringLength(50)]
        public string? IdClient { get; set; }

        /// <summary>Optional economic activity code used for electronic invoicing.</summary>
        [StringLength(50)]
        public string? EconomicActivityCode { get; set; }

        public DateTime? CreatedAt { get; set; }
    }
}
