using System.ComponentModel.DataAnnotations;

namespace MechanicApp.Server.Models
{
    public class Part
    {
        public int Id { get; set; }

        [Required, StringLength(150)]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "The Part Number field is required, please fill the field to continue!")]
        [StringLength(50)]
        public string? PartNumber { get; set; }

        [Required, StringLength(50)]
        public string Category { get; set; } = "General";

        [Required(ErrorMessage = "The Quantity field is required, please fill the field to continue!")]
        [Range(0, int.MaxValue)]
        public int Quantity { get; set; }

        [Required(ErrorMessage = "The Min Stock Level field is required, please fill the field to continue!")]
        [Range(0, int.MaxValue)]
        public int MinStock { get; set; } = 5;

        [Range(0.0, 9999999.99)]
        public decimal UnitCost { get; set; }

        [Range(0.0, 9999999.99)]
        public decimal SellPrice { get; set; }

        [StringLength(150)]
        public string? Supplier { get; set; }

        [StringLength(100)]
        public string? Location { get; set; }

        public int? CurrencyId { get; set; }
        public string? CurrencySymbol { get; set; }

        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
