using System.Text.RegularExpressions;
using Desk.Models;
using Microsoft.AspNetCore.Mvc;

namespace Desk.Pages.Companies;

public class IndexModel(ApiManager apiManager, SessionManager sessionManager, DeskConfig config)
    : AppPageModel(apiManager, sessionManager, config)
{
    [BindProperty]
    public Company CompanyInput { get; set; } = new();

    public async Task<IActionResult> OnGetListAsync([FromQuery] int page = 1, int pageSize = 100, string? sort = null,
        string? q = null)
    {
        try
        {
            string? extraQuery = null;
            if (q is not null)
                extraQuery = $"q={Uri.EscapeDataString(q)}";

            var (companies, totalCount) = await ApiManager.List<Company>(page, pageSize, sort, extraQuery);
            return new JsonResult(new { data = companies, totalCount });
        }
        catch (HttpRequestException ex)
        {
            return new JsonResult(new { error = ex.Message }) { StatusCode = (int?)ex.StatusCode ?? 500 };
        }
    }

    public async Task<IActionResult> OnPostAddAsync()
    {
        try
        {
            var result = await ApiManager.Add(CompanyInput);
            return new JsonResult(new { success = true, data = result });
        }
        catch (HttpRequestException ex)
        {
            return new JsonResult(new { error = ex.Message })
                { StatusCode = (int?)ex.StatusCode ?? 500 };
        }
    }

    public async Task<IActionResult> OnPostUpdateAsync()
    {
        try
        {
            var result = await ApiManager.Update(CompanyInput);
            return new JsonResult(new { success = true, data = result });
        }
        catch (HttpRequestException ex)
        {
            return new JsonResult(new { error = ex.Message })
                { StatusCode = (int?)ex.StatusCode ?? 500 };
        }
    }

    private static readonly Regex InvoiceCountRegex = new(@"(\d+)\D+(\d+)");

    public async Task<IActionResult> OnPostDeleteAsync(int id, bool force = false)
    {
        try
        {
            await ApiManager.Delete<Company>(id, force);
            return new JsonResult(new { success = true });
        }
        catch (HttpRequestException ex)
        {
            var match = InvoiceCountRegex.Match(ex.Message);
            if (match.Success && !force)
            {
                return new JsonResult(new
                {
                    error = ex.Message,
                    hasLinkedInvoices = true,
                    sentCount = match.Groups[1].Value,
                    receivedCount = match.Groups[2].Value
                }) { StatusCode = (int?)ex.StatusCode ?? 500 };
            }

            return new JsonResult(new { error = ex.Message })
                { StatusCode = (int?)ex.StatusCode ?? 500 };
        }
    }
}
