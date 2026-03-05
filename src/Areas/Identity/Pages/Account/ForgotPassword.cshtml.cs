using System.ComponentModel.DataAnnotations;
using System.Text.Encodings.Web;
using Desk.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Desk.Areas.Identity.Pages.Account;

[AllowAnonymous]
public class ForgotPasswordModel(
    UserManager<DeskUser> userManager,
    EmailService emailService,
    DeskConfig config) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public bool SmtpConfigured => config.Smtp.IsConfigured;

    public class InputModel
    {
        [Required(ErrorMessage = "Validation_Required")]
        [EmailAddress(ErrorMessage = "Validation_EmailInvalid")]
        public string Email { get; set; } = "";
    }

    public IActionResult OnGet()
    {
        if (!SmtpConfigured)
            return RedirectToPage("/Account/Login");

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!SmtpConfigured)
            return RedirectToPage("/Account/Login");

        if (!ModelState.IsValid)
            return Page();

        var user = await userManager.FindByEmailAsync(Input.Email);
        if (user is not null)
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            var callbackUrl = Url.Page(
                "/Account/ResetPassword",
                pageHandler: null,
                values: new { area = "Identity", token, email = Input.Email },
                protocol: Request.Scheme)!;

            await emailService.SendPasswordResetAsync(Input.Email, HtmlEncoder.Default.Encode(callbackUrl));
        }

        // Always redirect to confirmation (don't reveal if email exists)
        return RedirectToPage("/Account/ForgotPasswordConfirmation");
    }
}
