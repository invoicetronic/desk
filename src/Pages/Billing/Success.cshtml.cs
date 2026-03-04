using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Desk.Pages.Billing;

[Authorize]
public class SuccessModel(DeskConfig config) : PageModel
{
    public DeskConfig Config => config;

    public IActionResult OnGet()
    {
        if (!config.IsBillingEnabled)
            return RedirectToPage("/Index");

        return Page();
    }
}
