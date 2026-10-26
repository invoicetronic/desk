using System.ComponentModel.DataAnnotations;
using Desk.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Desk.Areas.Identity.Pages.Account;

[AllowAnonymous]
public class LoginWith2faModel(
    SignInManager<DeskUser> signInManager,
    SessionManager sessionManager,
    ApiKeyProtector apiKeyProtector,
    ILogger<LoginWith2faModel> logger) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public bool RememberMe { get; set; }
    public string? ReturnUrl { get; set; }
    public string? ErrorMessage { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Validation_Required")]
        [StringLength(7, MinimumLength = 6, ErrorMessage = "Mfa_CodeLength")]
        public string TwoFactorCode { get; set; } = "";

        public bool RememberMachine { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(bool rememberMe, string? returnUrl = null)
    {
        // Only reachable after a successful password check, which leaves the pending user in a cookie
        if (await signInManager.GetTwoFactorAuthenticationUserAsync() is null)
            return RedirectToPage("./Login");

        ReturnUrl = returnUrl;
        RememberMe = rememberMe;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(bool rememberMe, string? returnUrl = null)
    {
        ReturnUrl = returnUrl;
        RememberMe = rememberMe;
        returnUrl ??= Url.Content("~/");

        var user = await signInManager.GetTwoFactorAuthenticationUserAsync();
        if (user is null)
            return RedirectToPage("./Login");

        if (!ModelState.IsValid) return Page();

        var code = Input.TwoFactorCode.Replace(" ", string.Empty).Replace("-", string.Empty);
        var result = await signInManager.TwoFactorAuthenticatorSignInAsync(code, rememberMe, Input.RememberMachine);

        if (result.Succeeded)
        {
            if (user.ApiKey is not null)
                sessionManager.SetApiKey(apiKeyProtector.Unprotect(user.ApiKey));

            return LocalRedirect(returnUrl);
        }

        if (result.IsLockedOut)
        {
            logger.LogWarning("User {UserId} locked out after invalid authenticator codes", user.Id);
            ErrorMessage = "Mfa_LockedOut";
            return Page();
        }

        logger.LogInformation("Invalid authenticator code for user {UserId}", user.Id);
        ErrorMessage = "Mfa_InvalidCode";
        return Page();
    }
}
