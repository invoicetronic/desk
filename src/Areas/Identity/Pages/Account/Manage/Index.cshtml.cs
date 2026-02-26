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
    SessionManager sessionManager) : PageModel
{
    [BindProperty]
    public string? ApiKeyInput { get; set; }

    [BindProperty]
    public string? DisplayNameInput { get; set; }

    public string? SuccessMessage { get; set; }
    public string? ErrorMessage { get; set; }
    public Status? AccountStatus { get; set; }
    public string? CurrentEmail { get; set; }

    public async Task<IActionResult> OnGetAsync(bool apiKeyRequired = false)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            await signInManager.SignOutAsync();
            return RedirectToPage("/Account/Login", new { area = "Identity" });
        }

        if (apiKeyRequired)
            ErrorMessage = "Profile_ApiKeyRequired";

        ApiKeyInput = user.ApiKey;
        DisplayNameInput = user.DisplayName;
        CurrentEmail = user.Email;

        if (!string.IsNullOrEmpty(user.ApiKey))
        {
            try
            {
                sessionManager.SetApiKey(user.ApiKey);
                AccountStatus = await apiManager.GetStatus();
            }
            catch
            {
                // API unreachable, just don't show status
            }
        }

        return Page();
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
            // Session already has the key from validation above
            // Clear cached companies so they reload with new key
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
            catch { /* ignore */ }
        }

        return Page();
    }
}
