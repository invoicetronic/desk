using System.ComponentModel.DataAnnotations;
using Desk.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Desk.Areas.Identity.Pages.Account;

[AllowAnonymous]
public class ResetPasswordModel(UserManager<DeskUser> userManager) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required(ErrorMessage = "Validation_Required")]
        [EmailAddress(ErrorMessage = "Validation_EmailInvalid")]
        public string Email { get; set; } = "";

        [Required(ErrorMessage = "Validation_Required")]
        [StringLength(100, ErrorMessage = "Validation_StringLength", MinimumLength = 6)]
        [DataType(DataType.Password)]
        public string Password { get; set; } = "";

        [DataType(DataType.Password)]
        [Compare("Password", ErrorMessage = "Validation_PasswordMismatch")]
        public string ConfirmPassword { get; set; } = "";

        public string Token { get; set; } = "";
    }

    public IActionResult OnGet(string? token = null, string? email = null)
    {
        if (token is null)
            return BadRequest();

        Input = new InputModel { Token = token, Email = email ?? "" };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        var user = await userManager.FindByEmailAsync(Input.Email);
        if (user is null)
        {
            // Don't reveal that the user does not exist
            return RedirectToPage("/Account/ResetPasswordConfirmation");
        }

        var result = await userManager.ResetPasswordAsync(user, Input.Token, Input.Password);
        if (result.Succeeded)
            return RedirectToPage("/Account/ResetPasswordConfirmation");

        foreach (var error in result.Errors)
            ModelState.AddModelError(string.Empty, error.Description);

        return Page();
    }
}
