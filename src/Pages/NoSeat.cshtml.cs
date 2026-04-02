using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Desk.Pages;

[Authorize]
public class NoSeatModel(DeskConfig config) : PageModel
{
    public DeskConfig Config => config;

    public void OnGet()
    {
    }
}
