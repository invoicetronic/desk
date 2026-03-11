using System.ComponentModel.DataAnnotations;
using Desk.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Desk.Areas.Identity.Pages.Account;

[AllowAnonymous]
public class LoginModel(
    SignInManager<DeskUser> signInManager,
    UserManager<DeskUser> userManager,
    SessionManager sessionManager,
    ApiKeyProtector apiKeyProtector) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ReturnUrl { get; set; }
    public string? ErrorMessage { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Validation_Required")]
        [EmailAddress(ErrorMessage = "Validation_EmailInvalid")]
        public string Email { get; set; } = "";

        [Required(ErrorMessage = "Validation_Required")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = "";

        public bool RememberMe { get; set; }
    }

    public async Task OnGetAsync(string? returnUrl = null)
    {
        ReturnUrl = returnUrl ?? Url.Content("~/");
        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        returnUrl ??= Url.Content("~/");

        if (!ModelState.IsValid) return Page();

        var result = await signInManager.PasswordSignInAsync(
            Input.Email, Input.Password, Input.RememberMe, lockoutOnFailure: false);

        if (result.Succeeded)
        {
            var user = await userManager.FindByEmailAsync(Input.Email);
            if (user?.ApiKey is not null)
                sessionManager.SetApiKey(apiKeyProtector.Unprotect(user.ApiKey));

            return LocalRedirect(returnUrl);
        }

        ErrorMessage = "Auth_InvalidLogin";
        return Page();
    }
}
