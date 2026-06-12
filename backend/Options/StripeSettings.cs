namespace MechanicApp.Server.Options
{
    /// <summary>
    /// Strongly-typed configuration for Stripe payment gateway integration.
    /// Bound from the "Stripe" section in appsettings.json.
    /// </summary>
    public class StripeSettings
    {
        /// <summary>Configuration section name.</summary>
        public const string SectionName = "Stripe";

        /// <summary>The Stripe webhook signing secret (whsec_...).</summary>
        public string WebhookSecret { get; set; } = string.Empty;

        /// <summary>Legacy fallback payment link. Used when no plan-specific link is configured.</summary>
        public string PaymentLinkUrl { get; set; } = string.Empty;

        /// <summary>The Stripe Payment Link URL for the Standard plan.</summary>
        public string StandardPaymentLinkUrl { get; set; } = string.Empty;

        /// <summary>The Stripe Payment Link URL for the Premium plan.</summary>
        public string PremiumPaymentLinkUrl { get; set; } = string.Empty;

        /// <summary>The Stripe Payment Link URL for the Platinum plan.</summary>
        public string PlatinumPaymentLinkUrl { get; set; } = string.Empty;
    }
}
