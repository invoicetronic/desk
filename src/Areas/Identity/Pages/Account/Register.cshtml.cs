using System.ComponentModel.DataAnnotations;
using Desk.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Desk.Areas.Identity.Pages.Account;

[AllowAnonymous]
public class RegisterModel(
    UserManager<DeskUser> userManager,
    SignInManager<DeskUser> signInManager,
    DeskConfig config) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty]
    public BillingProfileModel? BillingProfile { get; set; }

    public bool IsBillingEnabled => config.IsBillingEnabled;

    public string? ReturnUrl { get; set; }
    public string? ErrorMessage { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Validation_Required")]
        [EmailAddress(ErrorMessage = "Validation_EmailInvalid")]
        public string Email { get; set; } = "";

        [Required(ErrorMessage = "Validation_Required")]
        public string? DisplayName { get; set; }

        [Required(ErrorMessage = "Validation_Required")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Validation_StringLength")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = "";

        [Required(ErrorMessage = "Validation_Required")]
        [DataType(DataType.Password)]
        [Compare("Password", ErrorMessage = "Validation_PasswordMismatch")]
        public string ConfirmPassword { get; set; } = "";
    }

    public void OnGet(string? returnUrl = null)
    {
        ReturnUrl = returnUrl ?? Url.Content("~/");
        if (IsBillingEnabled)
            BillingProfile = new();
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        returnUrl ??= Url.Content("~/");

        if (!IsBillingEnabled && BillingProfile is not null)
        {
            var billingKeys = typeof(BillingProfileModel).GetProperties().Select(p => p.Name).ToHashSet();
            foreach (var key in ModelState.Keys.Where(billingKeys.Contains).ToList())
                ModelState.Remove(key);
        }

        if (!ModelState.IsValid) return Page();

        var user = new DeskUser
        {
            UserName = Input.Email,
            Email = Input.Email,
            DisplayName = Input.DisplayName
        };

        if (IsBillingEnabled && BillingProfile is not null)
        {
            user.CompanyName = BillingProfile.CompanyName;
            user.TaxId = BillingProfile.TaxId;
            user.Address = BillingProfile.Address;
            user.City = BillingProfile.City;
            user.State = BillingProfile.State;
            user.ZipCode = BillingProfile.ZipCode;
            user.Country = BillingProfile.Country;
            user.PecMail = BillingProfile.PecMail;
            user.CodiceDestinatario = BillingProfile.CodiceDestinatario;
            user.PhoneNumber = BillingProfile.PhoneNumber;
        }

        var result = await userManager.CreateAsync(user, Input.Password);

        if (result.Succeeded)
        {
            await signInManager.SignInAsync(user, isPersistent: false);
            return LocalRedirect("/Identity/Account/Manage");
        }

        foreach (var error in result.Errors)
            ModelState.AddModelError(string.Empty, error.Description);

        return Page();
    }
}
