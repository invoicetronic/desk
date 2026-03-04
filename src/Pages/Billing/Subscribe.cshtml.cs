using Desk.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Desk.Pages.Billing;

[Authorize]
public class SubscribeModel(
    UserManager<DeskUser> userManager,
    DeskConfig config) : PageModel
{
    public DeskConfig Config => config;
    public string? StripeCustomerId { get; set; }
    public bool IsPreviewExpired { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!config.IsBillingEnabled)
            return RedirectToPage("/Index");

        var user = await userManager.GetUserAsync(User);
        if (user is null)
            return RedirectToPage("/Account/Login", new { area = "Identity" });

        if (user.SubscriptionStatus is "active" or "trialing")
            return RedirectToPage("/Index");

        StripeCustomerId = user.StripeCustomerId;
        var daysLeft = AppPageModel.PreviewDays - (int)(DateTime.UtcNow - user.CreatedAt).TotalDays;
        IsPreviewExpired = daysLeft <= 0;
        return Page();
    }

    public async Task<IActionResult> OnPostCheckoutAsync()
    {
        if (!config.IsBillingEnabled)
            return RedirectToPage("/Index");

        var user = await userManager.GetUserAsync(User);
        if (user is null)
            return RedirectToPage("/Account/Login", new { area = "Identity" });

        var stripeService = HttpContext.RequestServices.GetService<StripeService>();
        if (stripeService is null)
            return RedirectToPage("/Index");

        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var session = await stripeService.CreateCheckoutSessionAsync(
            user.Id,
            user.Email!,
            user.TaxId,
            $"{baseUrl}/Billing/Success",
            $"{baseUrl}/Billing/Subscribe");

        return Redirect(session.Url);
    }

    public async Task<IActionResult> OnPostPortalAsync()
    {
        if (!config.IsBillingEnabled)
            return RedirectToPage("/Index");

        var user = await userManager.GetUserAsync(User);
        if (user?.StripeCustomerId is null)
            return RedirectToPage("/Billing/Subscribe");

        var stripeService = HttpContext.RequestServices.GetService<StripeService>();
        if (stripeService is null)
            return RedirectToPage("/Index");

        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var session = await stripeService.CreatePortalSessionAsync(
            user.StripeCustomerId,
            $"{baseUrl}/Billing/Subscribe");

        return Redirect(session.Url);
    }
}
