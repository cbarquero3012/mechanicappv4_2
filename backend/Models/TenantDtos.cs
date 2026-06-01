using System.ComponentModel.DataAnnotations;

namespace MechanicApp.Server.Models
{
    public class CreateTenantRequest
    {
        [Required]
        [StringLength(100, MinimumLength = 2)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [StringLength(254)]
        public string Email { get; set; } = string.Empty;

        public string? PlanName { get; set; }
        public string? Country { get; set; }
    }

    public class CreateDemoRequest
    {
        [StringLength(100)]
        public string? Name { get; set; }

        [EmailAddress]
        [StringLength(254)]
        public string? Email { get; set; }

        [StringLength(50)]
        public string? Username { get; set; }
    }

    public class TenantOnboardRequest
    {
        [Required]
        [EmailAddress]
        [StringLength(254)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(100, MinimumLength = 2)]
        public string CompanyName { get; set; } = string.Empty;

        [Required]
        [StringLength(128, MinimumLength = 8)]
        public string AdminPassword { get; set; } = string.Empty;

        [StringLength(50)]
        public string? Username { get; set; }

        public string? PlanName { get; set; }
    }

    public class ConvertTenantRequest
    {
        public string? PlanName { get; set; }
        public string? StripeSubscriptionId { get; set; }
    }
}
