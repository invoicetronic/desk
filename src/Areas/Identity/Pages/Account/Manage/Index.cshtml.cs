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
    ApiKeyProtector apiKeyProtector,
    ILogger<IndexModel> logger) : PageModel
{
    public DeskConfig Config => config;
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

        var plainApiKey = apiKeyProtector.UnprotectOrNull(user.ApiKey);
        ApiKeyInput = plainApiKey;
        DisplayNameInput = user.DisplayName;
        CurrentEmail = user.Email;

        if (plainApiKey is not null)
        {
            try
            {
                sessionManager.SetApiKey(plainApiKey);
                AccountStatus = await apiManager.GetStatus();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to load API status");
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
            logger.LogInformation("API key save rejected for user {UserId} ({Email}): empty input",
                user.Id, user.Email);
            ErrorMessage = "Profile_ApiKeyMissing";
            return Page();
        }

        // Validate by calling the status endpoint with the new key
        try
        {
            sessionManager.SetApiKey(ApiKeyInput);
            AccountStatus = await apiManager.GetStatus();
        }
        catch (Exception ex)
        {
            logger.LogInformation(ex, "API key save rejected for user {UserId} ({Email}): validation failed (invalid key or API unreachable)",
                user.Id, user.Email);
            ErrorMessage = "Profile_ApiKeyInvalid";
            var existingKey = apiKeyProtector.UnprotectOrNull(user.ApiKey) ?? "";
            sessionManager.SetApiKey(existingKey);
            return Page();
        }

        // Block save if the key is valid but has no active Desk seat (hosted mode only)
        if (!Config.IsStandalone && AccountStatus is { HasActiveSeat: false })
        {
            logger.LogInformation("API key save rejected for user {UserId} ({Email}): valid key but no active Desk seat",
                user.Id, user.Email);
            ErrorMessage = "Profile_ApiKeyNoSeat";
            var existingKey = apiKeyProtector.UnprotectOrNull(user.ApiKey) ?? "";
            sessionManager.SetApiKey(existingKey);
            return Page();
        }

        user.ApiKey = apiKeyProtector.Protect(ApiKeyInput);
        var result = await userManager.UpdateAsync(user);

        if (result.Succeeded)
        {
            sessionManager.ClearCompanies();
            sessionManager.ClearHasActiveSeat();
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
        var plainApiKey = apiKeyProtector.UnprotectOrNull(user.ApiKey);
        ApiKeyInput = plainApiKey;

        user.DisplayName = DisplayNameInput;
        var result = await userManager.UpdateAsync(user);

        if (result.Succeeded)
            SuccessMessage = "Profile_ProfileSaved";
        else
            ErrorMessage = string.Join(", ", result.Errors.Select(e => e.Description));

        if (plainApiKey is not null)
        {
            try
            {
                sessionManager.SetApiKey(plainApiKey);
                AccountStatus = await apiManager.GetStatus();
            }
            catch (Exception ex) { logger.LogWarning(ex, "Failed to load API status"); }
        }

        return Page();
    }
}
