using Desk.Data;
using Desk.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Desk.Areas.Identity.Pages.Account.Manage;

[Authorize]
public class IndexModel(
    UserManager<DeskUser> userManager,
    SignInManager<DeskUser> signInManager,
    ApiManager apiManager,
    SessionManager sessionManager,
    DeskConfig config,
    ILogger<IndexModel> logger) : PageModel
{
    public DeskConfig Config => config;
    [BindProperty]
    public string? ApiKeyInput { get; set; }

    [BindProperty]
    public string? DisplayNameInput { get; set; }

    [BindProperty]
    public BillingProfileModel? BillingProfile { get; set; }

    public string? SuccessMessage { get; set; }
    public string? ErrorMessage { get; set; }
    public Status? AccountStatus { get; set; }
    public string? CurrentEmail { get; set; }
    public string? UserSubscriptionStatus { get; set; }
    public string? UserStripeCustomerId { get; set; }

    public async Task<IActionResult> OnGetAsync(bool apiKeyRequired = false, bool billingProfileRequired = false)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            await signInManager.SignOutAsync();
            return RedirectToPage("/Account/Login", new { area = "Identity" });
        }

        if (apiKeyRequired)
            ErrorMessage = "Profile_ApiKeyRequired";
        else if (billingProfileRequired)
            ErrorMessage = "Profile_BillingProfileRequired";

        ApiKeyInput = user.ApiKey;
        DisplayNameInput = user.DisplayName;
        CurrentEmail = user.Email;
        UserSubscriptionStatus = user.SubscriptionStatus;
        UserStripeCustomerId = user.StripeCustomerId;

        if (config.IsBillingEnabled)
            PopulateBillingProfile(user);

        if (!string.IsNullOrEmpty(user.ApiKey))
        {
            try
            {
                sessionManager.SetApiKey(user.ApiKey);
                AccountStatus = await apiManager.GetStatus();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to load API status");
            }
        }

        return Page();
    }

    private void PopulateBillingProfile(DeskUser user)
    {
        BillingProfile = new BillingProfileModel
        {
            CompanyName = user.CompanyName ?? "",
            TaxId = user.TaxId ?? "",
            Address = user.Address ?? "",
            City = user.City ?? "",
            State = user.State,
            ZipCode = user.ZipCode ?? "",
            Country = user.Country ?? BillingProfileModel.DefaultCountry,
            PecMail = user.PecMail,
            CodiceDestinatario = user.CodiceDestinatario,
            PhoneNumber = user.PhoneNumber,
            IsTaxIdReadOnly = user.SubscriptionStatus is "active" or "trialing"
        };
    }

    public async Task<IActionResult> OnPostSaveApiKeyAsync()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            await signInManager.SignOutAsync();
            return RedirectToPage("/Account/Login", new { area = "Identity" });
        }

        CurrentEmail = user.Email;
        DisplayNameInput = user.DisplayName;

        if (string.IsNullOrWhiteSpace(ApiKeyInput))
        {
            ErrorMessage = "API key is required.";
            return Page();
        }

        // Validate by calling the status endpoint with the new key
        try
        {
            sessionManager.SetApiKey(ApiKeyInput);
            AccountStatus = await apiManager.GetStatus();
        }
        catch
        {
            ErrorMessage = "Profile_ApiKeyInvalid";
            sessionManager.SetApiKey(user.ApiKey ?? "");
            return Page();
        }

        user.ApiKey = ApiKeyInput;
        var result = await userManager.UpdateAsync(user);

        if (result.Succeeded)
        {
            sessionManager.ClearCompanies();
            SuccessMessage = "Profile_ApiKeySaved";
        }
        else
        {
            ErrorMessage = string.Join(", ", result.Errors.Select(e => e.Description));
        }

        return Page();
    }

    public async Task<IActionResult> OnPostSaveProfileAsync()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            await signInManager.SignOutAsync();
            return RedirectToPage("/Account/Login", new { area = "Identity" });
        }

        CurrentEmail = user.Email;
        ApiKeyInput = user.ApiKey;

        user.DisplayName = DisplayNameInput;
        var result = await userManager.UpdateAsync(user);

        if (result.Succeeded)
            SuccessMessage = "Profile_ProfileSaved";
        else
            ErrorMessage = string.Join(", ", result.Errors.Select(e => e.Description));

        if (!string.IsNullOrEmpty(user.ApiKey))
        {
            try
            {
                sessionManager.SetApiKey(user.ApiKey);
                AccountStatus = await apiManager.GetStatus();
            }
            catch (Exception ex) { logger.LogWarning(ex, "Failed to load API status"); }
        }

        return Page();
    }

    public async Task<IActionResult> OnPostSaveBillingInfoAsync()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            await signInManager.SignOutAsync();
            return RedirectToPage("/Account/Login", new { area = "Identity" });
        }

        CurrentEmail = user.Email;
        ApiKeyInput = user.ApiKey;
        DisplayNameInput = user.DisplayName;
        UserSubscriptionStatus = user.SubscriptionStatus;
        UserStripeCustomerId = user.StripeCustomerId;

        if (BillingProfile is null || !ModelState.IsValid)
        {
            if (BillingProfile is not null)
                BillingProfile.IsTaxIdReadOnly = user.SubscriptionStatus is "active" or "trialing";
            if (!string.IsNullOrEmpty(user.ApiKey))
            {
                try
                {
                    sessionManager.SetApiKey(user.ApiKey);
                    AccountStatus = await apiManager.GetStatus();
                }
                catch (Exception ex) { logger.LogWarning(ex, "Failed to load API status"); }
            }
            return Page();
        }

        user.CompanyName = BillingProfile.CompanyName;
        if (!BillingProfile.IsTaxIdReadOnly)
            user.TaxId = BillingProfile.TaxId;
        user.Address = BillingProfile.Address;
        user.City = BillingProfile.City;
        user.State = BillingProfile.State;
        user.ZipCode = BillingProfile.ZipCode;
        user.Country = BillingProfile.Country;
        user.PecMail = BillingProfile.PecMail;
        user.CodiceDestinatario = BillingProfile.CodiceDestinatario;
        user.PhoneNumber = BillingProfile.PhoneNumber;

        var result = await userManager.UpdateAsync(user);

        if (result.Succeeded)
        {
            SuccessMessage = "Profile_BillingInfoSaved";

            if (!string.IsNullOrEmpty(user.StripeCustomerId))
            {
                var stripeService = HttpContext.RequestServices.GetService<StripeService>();
                if (stripeService is not null)
                {
                    try { await stripeService.CreateOrUpdateCustomerAsync(user); } catch (Exception ex) { logger.LogWarning(ex, "Failed to update Stripe customer for user {UserId}", user.Id); }
                }
            }
        }
        else
            ErrorMessage = string.Join(", ", result.Errors.Select(e => e.Description));

        PopulateBillingProfile(user);

        if (!string.IsNullOrEmpty(user.ApiKey))
        {
            try
            {
                sessionManager.SetApiKey(user.ApiKey);
                AccountStatus = await apiManager.GetStatus();
            }
            catch (Exception ex) { logger.LogWarning(ex, "Failed to load API status"); }
        }

        return Page();
    }

    public async Task<IActionResult> OnPostManageSubscriptionAsync()
    {
        var user = await userManager.GetUserAsync(User);
        if (user?.StripeCustomerId is null)
            return RedirectToPage();

        var stripeService = HttpContext.RequestServices.GetService<StripeService>();
        if (stripeService is null)
            return RedirectToPage();

        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var session = await stripeService.CreatePortalSessionAsync(
            user.StripeCustomerId,
            $"{baseUrl}/Identity/Account/Manage");

        return Redirect(session.Url);
    }
}
