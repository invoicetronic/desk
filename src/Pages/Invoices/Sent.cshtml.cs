using Desk.Models;
using Microsoft.AspNetCore.Mvc;

namespace Desk.Pages.Invoices;

public class SentModel(ApiManager apiManager, SessionManager sessionManager, DeskConfig config)
    : AppPageModel(apiManager, sessionManager, config)
{
    public async Task<IActionResult> OnGetListAsync(
        [FromQuery] int page = 1, int pageSize = 20, string? sort = null,
        string? dateFrom = null, string? dateTo = null, string? q = null)
    {
        try
        {
            var filters = new List<string>();

            if (CompanyFilter is not null)
                filters.Add(CompanyFilter);

            if (dateFrom is not null)
                filters.Add($"date_sent_from={dateFrom}T00:00:00Z");

            if (dateTo is not null)
                filters.Add($"date_sent_to={dateTo}T23:59:59Z");

            if (q is not null)
                filters.Add($"q={Uri.EscapeDataString(q)}");

            var extraQuery = filters.Count > 0 ? string.Join("&", filters) : null;

            var (invoices, totalCount) = await ApiManager.List<Send>(page, pageSize, sort, extraQuery);

            var enriched = invoices?.Select(inv => new
            {
                inv.Id,
                inv.Created,
                inv.Version,
                inv.UserId,
                inv.CompanyId,
                inv.Committente,
                inv.NomeCommittente,
                inv.Prestatore,
                inv.Identifier,
                inv.FileName,
                inv.Format,
                inv.LastUpdate,
                inv.DateSent,
                inv.Documents,
                State = inv.LatestState?.ToString() ?? ""
            }).ToList();

            return new JsonResult(new { data = enriched, totalCount });
        }
        catch (HttpRequestException ex)
        {
            return new JsonResult(new { error = ex.Message })
                { StatusCode = (int?)ex.StatusCode ?? 500 };
        }
    }
}
