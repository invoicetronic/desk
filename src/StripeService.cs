using Desk.Data;
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

    public async Task<Session> CreateCheckoutSessionAsync(DeskUser user, string successUrl, string cancelUrl)
    {
        var customerId = user.StripeCustomerId ?? (await CreateOrUpdateCustomerAsync(user)).Id;

        var priceId = user.TaxId?.StartsWith("IT", StringComparison.OrdinalIgnoreCase) == true
            ? _config.Stripe.PriceIdIt
            : _config.Stripe.PriceIdForeign;

        var options = new SessionCreateOptions
        {
            Customer = customerId,
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
                Metadata = new Dictionary<string, string> { ["desk_user_id"] = user.Id }
            },
            Metadata = new Dictionary<string, string> { ["desk_user_id"] = user.Id },
            SuccessUrl = successUrl,
            CancelUrl = cancelUrl
        };

        var service = new SessionService();
        return await service.CreateAsync(options);
    }

    public async Task<Customer> CreateOrUpdateCustomerAsync(DeskUser user)
    {
        var address = new AddressOptions
        {
            Line1 = user.Address,
            City = user.City,
            State = user.State,
            PostalCode = user.ZipCode,
            Country = user.Country
        };

        if (!string.IsNullOrEmpty(user.StripeCustomerId))
        {
            var updateOptions = new CustomerUpdateOptions
            {
                Name = user.CompanyName,
                Email = user.Email,
                Phone = user.PhoneNumber,
                Address = address,
                Metadata = new Dictionary<string, string>
                {
                    ["desk_user_id"] = user.Id,
                    ["tax_id"] = user.TaxId ?? "",
                    ["pec_mail"] = user.PecMail ?? "",
                    ["codice_destinatario"] = user.CodiceDestinatario ?? ""
                }
            };

            var service = new CustomerService();
            return await service.UpdateAsync(user.StripeCustomerId, updateOptions);
        }
        else
        {
            var createOptions = new CustomerCreateOptions
            {
                Name = user.CompanyName,
                Email = user.Email,
                Phone = user.PhoneNumber,
                Address = address,
                Metadata = new Dictionary<string, string>
                {
                    ["desk_user_id"] = user.Id,
                    ["tax_id"] = user.TaxId ?? "",
                    ["pec_mail"] = user.PecMail ?? "",
                    ["codice_destinatario"] = user.CodiceDestinatario ?? ""
                }
            };

            var service = new CustomerService();
            return await service.CreateAsync(createOptions);
        }
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
        return EventUtility.ConstructEvent(json, signature, _config.Stripe.WebhookSecret, throwOnApiVersionMismatch: false);
    }
}
