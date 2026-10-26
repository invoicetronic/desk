using Desk.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Desk.Areas.Identity.Pages.Account.Manage;

[Authorize]
public class TwoFactorModel(
    UserManager<DeskUser> userManager,
    SignInManager<DeskUser> signInManager,
    ILogger<TwoFactorModel> logger) : PageModel
{
    public const int RecoveryCodeCount = 10;

    public bool IsEnabled { get; set; }
    public int RecoveryCodesLeft { get; set; }
    public bool IsMachineRemembered { get; set; }

    /// <summary>
    /// Set right after enabling or regenerating: the codes are shown once and never stored in clear.
    /// </summary>
    [TempData]
    public string[]? RecoveryCodes { get; set; }

    [TempData]
    public string? SuccessMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
            return await SignOutAndRedirect();

        await LoadAsync(user);
        return Page();
    }

    public async Task<IActionResult> OnPostDisableAsync()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
            return await SignOutAndRedirect();

        _ = await userManager.SetTwoFactorEnabledAsync(user, false);
        // A new key on the next activation: whoever saw the old one (or holds the old device) gets nothing
        _ = await userManager.ResetAuthenticatorKeyAsync(user);
        // Both calls rotate the security stamp, which would otherwise sign the user out
        await signInManager.RefreshSignInAsync(user);
        await signInManager.ForgetTwoFactorClientAsync();

        logger.LogInformation("User {UserId} disabled two-factor authentication", user.Id);
        SuccessMessage = "Mfa_Disabled";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostGenerateRecoveryCodesAsync()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
            return await SignOutAndRedirect();

        if (!await userManager.GetTwoFactorEnabledAsync(user))
            return RedirectToPage();

        RecoveryCodes = [.. await userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, RecoveryCodeCount) ?? []];
        logger.LogInformation("User {UserId} generated new recovery codes", user.Id);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostForgetBrowserAsync()
    {
        await signInManager.ForgetTwoFactorClientAsync();
        SuccessMessage = "Mfa_BrowserForgotten";
        return RedirectToPage();
    }

    private async Task LoadAsync(DeskUser user)
    {
        IsEnabled = await userManager.GetTwoFactorEnabledAsync(user);
        RecoveryCodesLeft = await userManager.CountRecoveryCodesAsync(user);
        IsMachineRemembered = await signInManager.IsTwoFactorClientRememberedAsync(user);
    }

    private async Task<IActionResult> SignOutAndRedirect()
    {
        await signInManager.SignOutAsync();
        return RedirectToPage("/Account/Login", new { area = "Identity" });
    }
}
