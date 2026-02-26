using Microsoft.AspNetCore.Mvc;

namespace Desk.Pages.Export;

public class IndexModel(ApiManager apiManager, SessionManager sessionManager, DeskConfig config)
    : AppPageModel(apiManager, sessionManager, config)
{
    public async Task<IActionResult> OnGetDownloadAsync(
        string type, string? dateFrom = null, string? dateTo = null,
        int? year = null, int? month = null, int? quarter = null)
    {
        try
        {
            var qs = new List<string>();

            if (!string.IsNullOrEmpty(type))
                qs.Add($"type={type}");

            if (year is not null)
                qs.Add($"year={year}");

            if (month is not null)
                qs.Add($"month={month}");

            if (quarter is not null)
                qs.Add($"quarter={quarter}");

            if (dateFrom is not null)
                qs.Add($"document_date_from={dateFrom}");

            if (dateTo is not null)
                qs.Add($"document_date_to={dateTo}");

            if (CompanyFilter is not null)
                qs.Add(CompanyFilter);

            var queryString = string.Join("&", qs);
            var bytes = await ApiManager.Export(queryString);

            if (bytes is null or { Length: 0 })
                return new JsonResult(new { error = "No data to export." }) { StatusCode = 404 };

            var fileName = $"export_{type}_{DateTime.Now:yyyyMMdd_HHmmss}.zip";
            return File(bytes, "application/zip", fileName);
        }
        catch (HttpRequestException ex)
        {
            return new JsonResult(new { error = ex.Message })
                { StatusCode = (int?)ex.StatusCode ?? 500 };
        }
    }
}
