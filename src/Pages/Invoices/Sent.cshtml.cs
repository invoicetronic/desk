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

            // Fetch invoices and a first batch of recent updates in parallel
            var updateFilters = new List<string>();
            if (CompanyFilter is not null)
                updateFilters.Add(CompanyFilter);
            var updateQuery = updateFilters.Count > 0 ? string.Join("&", updateFilters) : null;

            var invoicesTask = ApiManager.List<Send>(page, pageSize, sort, extraQuery);
            var updatesTask = ApiManager.List<Update>(1, pageSize, "-last_update", updateQuery);
            await Task.WhenAll(invoicesTask, updatesTask);

            var (invoices, totalCount) = await invoicesTask;
            var (updates, _) = await updatesTask;

            // Build map from initial batch
            var stateMap = new Dictionary<int, string>();
            if (updates is not null)
            {
                foreach (var u in updates)
                    stateMap.TryAdd(u.SendId, u.State.ToString());
            }

            // For any displayed invoices still missing a state, fetch their updates directly
            var missing = invoices?.Where(i => !stateMap.ContainsKey(i.Id)).ToList() ?? [];
            if (missing.Count > 0)
            {
                var tasks = missing.Select(inv =>
                    ApiManager.List<Update>(1, 1, "-last_update", $"send_id={inv.Id}"));
                var results = await Task.WhenAll(tasks);
                foreach (var (result, _) in results)
                {
                    if (result is [var u, ..])
                        stateMap.TryAdd(u.SendId, u.State.ToString());
                }
            }

            // Enrich invoices with state
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
                State = stateMap.GetValueOrDefault(inv.Id, "")
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
