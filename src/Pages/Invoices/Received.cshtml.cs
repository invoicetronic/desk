using Desk.Models;
using Microsoft.AspNetCore.Mvc;

namespace Desk.Pages.Invoices;

public class ReceivedModel(ApiManager apiManager, SessionManager sessionManager, DeskConfig config)
    : AppPageModel(apiManager, sessionManager, config)
{
    public async Task<IActionResult> OnGetListAsync(
        int page = 1, int pageSize = 50, string? sort = null,
        bool unreadOnly = false, string? q = null)
    {
        try
        {
            var filters = new List<string>();

            if (CompanyFilter is not null)
                filters.Add(CompanyFilter);

            if (unreadOnly)
                filters.Add("unread=true");

            if (q is not null)
                filters.Add($"q={Uri.EscapeDataString(q)}");

            var extraQuery = filters.Count > 0 ? string.Join("&", filters) : null;

            var (invoices, totalCount) = await ApiManager.List<Receive>(page, pageSize, sort, extraQuery);
            return new JsonResult(new { data = invoices, totalCount });
        }
        catch (HttpRequestException ex)
        {
            return new JsonResult(new { error = ex.Message })
                { StatusCode = (int?)ex.StatusCode ?? 500 };
        }
    }
}
