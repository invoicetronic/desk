using Stripe;
using Stripe.Checkout;

namespace Desk;

public class StripeService
{
    private readonly DeskConfig _config;

    public StripeService(DeskConfig config)
    {
        _config = config;
        StripeConfiguration.ApiKey = config.Stripe.SecretKey;
    }

    public async Task<Session> CreateCheckoutSessionAsync(string userId, string email, string? taxId, string successUrl, string cancelUrl)
    {
        var priceId = taxId?.StartsWith("IT", StringComparison.OrdinalIgnoreCase) == true
            ? _config.Stripe.PriceIdIt
            : _config.Stripe.PriceIdForeign;

        var options = new SessionCreateOptions
        {
            CustomerEmail = email,
            PaymentMethodTypes = ["card"],
            Mode = "subscription",
            LineItems =
            [
                new SessionLineItemOptions
                {
                    Price = priceId,
                    Quantity = 1
                }
            ],
            SubscriptionData = new SessionSubscriptionDataOptions
            {
                Metadata = new Dictionary<string, string> { ["desk_user_id"] = userId }
            },
            Metadata = new Dictionary<string, string> { ["desk_user_id"] = userId },
            SuccessUrl = successUrl,
            CancelUrl = cancelUrl
        };

        var service = new SessionService();
        return await service.CreateAsync(options);
    }

    public async Task<Stripe.BillingPortal.Session> CreatePortalSessionAsync(string stripeCustomerId, string returnUrl)
    {
        var options = new Stripe.BillingPortal.SessionCreateOptions
        {
            Customer = stripeCustomerId,
            ReturnUrl = returnUrl
        };

        var service = new Stripe.BillingPortal.SessionService();
        return await service.CreateAsync(options);
    }

    public Event ConstructWebhookEvent(string json, string signature)
    {
        return EventUtility.ConstructEvent(json, signature, _config.Stripe.WebhookSecret);
    }
}
