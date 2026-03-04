using Desk.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Stripe;

namespace Desk;

public static class StripeWebhookEndpoint
{
    public static void MapStripeWebhook(this WebApplication app)
    {
        app.MapPost("/api/stripe/webhook", HandleWebhookAsync)
            .AllowAnonymous()
            .DisableAntiforgery();
    }

    private static async Task<IResult> HandleWebhookAsync(
        HttpContext context,
        StripeService stripeService,
        UserManager<DeskUser> userManager)
    {
        var json = await new StreamReader(context.Request.Body).ReadToEndAsync();
        var signature = context.Request.Headers["Stripe-Signature"].ToString();

        Event stripeEvent;
        try
        {
            stripeEvent = stripeService.ConstructWebhookEvent(json, signature);
        }
        catch (StripeException)
        {
            return Results.BadRequest("Invalid signature");
        }

        switch (stripeEvent.Type)
        {
            case EventTypes.CheckoutSessionCompleted:
            {
                if (stripeEvent.Data.Object is Stripe.Checkout.Session session)
                {
                    var userId = session.Metadata.GetValueOrDefault("desk_user_id");
                    if (userId is not null)
                    {
                        var user = await userManager.FindByIdAsync(userId);
                        if (user is not null)
                        {
                            user.StripeCustomerId = session.CustomerId;
                            user.SubscriptionStatus = "active";
                            await userManager.UpdateAsync(user);
                        }
                    }
                }
                break;
            }

            case EventTypes.CustomerSubscriptionUpdated:
            {
                if (stripeEvent.Data.Object is Subscription subscription)
                {
                    var user = await FindUserByCustomerIdAsync(userManager, subscription.CustomerId);
                    if (user is not null)
                    {
                        user.SubscriptionStatus = subscription.Status;
                        await userManager.UpdateAsync(user);
                    }
                }
                break;
            }

            case EventTypes.CustomerSubscriptionDeleted:
            {
                if (stripeEvent.Data.Object is Subscription subscription)
                {
                    var user = await FindUserByCustomerIdAsync(userManager, subscription.CustomerId);
                    if (user is not null)
                    {
                        user.SubscriptionStatus = "canceled";
                        await userManager.UpdateAsync(user);
                    }
                }
                break;
            }

            case EventTypes.InvoicePaymentFailed:
            {
                if (stripeEvent.Data.Object is Invoice invoice)
                {
                    var user = await FindUserByCustomerIdAsync(userManager, invoice.CustomerId);
                    if (user is not null)
                    {
                        user.SubscriptionStatus = "past_due";
                        await userManager.UpdateAsync(user);
                    }
                }
                break;
            }
        }

        return Results.Ok();
    }

    private static async Task<DeskUser?> FindUserByCustomerIdAsync(
        UserManager<DeskUser> userManager, string? customerId)
    {
        if (string.IsNullOrEmpty(customerId))
            return null;

        return userManager.Users.FirstOrDefault(u => u.StripeCustomerId == customerId);
    }
}
