using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Encodings.Web;
using Desk.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Desk.Areas.Identity.Pages.Account.Manage;

[Authorize]
public class EnableAuthenticatorModel(
    UserManager<DeskUser> userManager,
    SignInManager<DeskUser> signInManager,
    DeskConfig config,
    UrlEncoder urlEncoder,
    ILogger<EnableAuthenticatorModel> logger) : PageModel
{
    public string SharedKey { get; set; } = "";
    public string AuthenticatorUri { get; set; } = "";
    public string? ErrorMessage { get; set; }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    [TempData]
    public string[]? RecoveryCodes { get; set; }

    [TempData]
    public string? SuccessMessage { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Validation_Required")]
        [StringLength(7, MinimumLength = 6, ErrorMessage = "Mfa_CodeLength")]
        public string Code { get; set; } = "";
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
            return await SignOutAndRedirect();

        if (await userManager.GetTwoFactorEnabledAsync(user))
            return RedirectToPage("./TwoFactor");

        await LoadSharedKeyAsync(user);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
            return await SignOutAndRedirect();

        if (!ModelState.IsValid)
        {
            await LoadSharedKeyAsync(user);
            return Page();
        }

        var code = Input.Code.Replace(" ", string.Empty).Replace("-", string.Empty);
        var valid = await userManager.VerifyTwoFactorTokenAsync(
            user, userManager.Options.Tokens.AuthenticatorTokenProvider, code);

        if (!valid)
        {
            ErrorMessage = "Mfa_InvalidCode";
            await LoadSharedKeyAsync(user);
            return Page();
        }

        _ = await userManager.SetTwoFactorEnabledAsync(user, true);
        // Always a fresh set: codes left over from an earlier activation may have been seen by someone else
        RecoveryCodes = [.. await userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, TwoFactorModel.RecoveryCodeCount) ?? []];
        await signInManager.RefreshSignInAsync(user);

        logger.LogInformation("User {UserId} enabled two-factor authentication", user.Id);
        SuccessMessage = "Mfa_Enabled";
        return RedirectToPage("./TwoFactor");
    }

    private async Task LoadSharedKeyAsync(DeskUser user)
    {
        var key = await userManager.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrEmpty(key))
        {
            _ = await userManager.ResetAuthenticatorKeyAsync(user);
            // The reset rotates the security stamp: keep the current session valid
            await signInManager.RefreshSignInAsync(user);
            key = await userManager.GetAuthenticatorKeyAsync(user) ?? "";
        }

        SharedKey = FormatKey(key);
        // The issuer is what the authenticator app shows next to the code: use the (possibly white-label) app name
        var issuer = urlEncoder.Encode(config.Branding.AppName);
        AuthenticatorUri =
            $"otpauth://totp/{issuer}:{urlEncoder.Encode(user.Email ?? "")}?secret={key}&issuer={issuer}&digits=6";
    }

    private static string FormatKey(string key)
    {
        var result = new StringBuilder();
        for (var i = 0; i < key.Length; i += 4)
        {
            if (i > 0) _ = result.Append(' ');
            _ = result.Append(key.AsSpan(i, Math.Min(4, key.Length - i)));
        }

        return result.ToString().ToLowerInvariant();
    }

    private async Task<IActionResult> SignOutAndRedirect()
    {
        await signInManager.SignOutAsync();
        return RedirectToPage("/Account/Login", new { area = "Identity" });
    }
}
