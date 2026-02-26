using Desk.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Desk.Areas.Identity.Pages.Account;

public class LogoutModel(SignInManager<DeskUser> signInManager) : PageModel
{
    public async Task<IActionResult> OnPost()
    {
        await signInManager.SignOutAsync();
        return RedirectToPage("/Index");
    }
}
