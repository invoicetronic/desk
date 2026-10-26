using System.ComponentModel.DataAnnotations;
using Desk.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Desk.Areas.Identity.Pages.Account;

[AllowAnonymous]
public class LoginWithRecoveryCodeModel(
    SignInManager<DeskUser> signInManager,
    SessionManager sessionManager,
    ApiKeyProtector apiKeyProtector,
    ILogger<LoginWithRecoveryCodeModel> logger) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ReturnUrl { get; set; }
    public string? ErrorMessage { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Validation_Required")]
        public string RecoveryCode { get; set; } = "";
    }

    public async Task<IActionResult> OnGetAsync(string? returnUrl = null)
    {
        if (await signInManager.GetTwoFactorAuthenticationUserAsync() is null)
            return RedirectToPage("./Login");

        ReturnUrl = returnUrl;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        ReturnUrl = returnUrl;
        returnUrl ??= Url.Content("~/");

        var user = await signInManager.GetTwoFactorAuthenticationUserAsync();
        if (user is null)
            return RedirectToPage("./Login");

        if (!ModelState.IsValid) return Page();

        var code = Input.RecoveryCode.Replace(" ", string.Empty);
        var result = await signInManager.TwoFactorRecoveryCodeSignInAsync(code);

        if (result.Succeeded)
        {
            logger.LogInformation("User {UserId} logged in with a recovery code", user.Id);
            if (user.ApiKey is not null)
                sessionManager.SetApiKey(apiKeyProtector.Unprotect(user.ApiKey));

            return LocalRedirect(returnUrl);
        }

        if (result.IsLockedOut)
        {
            logger.LogWarning("User {UserId} locked out after invalid recovery codes", user.Id);
            ErrorMessage = "Mfa_LockedOut";
            return Page();
        }

        logger.LogInformation("Invalid recovery code for user {UserId}", user.Id);
        ErrorMessage = "Mfa_InvalidRecoveryCode";
        return Page();
    }
}
