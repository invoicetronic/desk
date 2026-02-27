using Desk.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Desk.Pages;

[Authorize]
public abstract class AppPageModel(
    ApiManager apiManager,
    SessionManager sessionManager,
    DeskConfig config) : PageModel
{
    protected ApiManager ApiManager { get; } = apiManager;
    protected SessionManager SessionManager { get; } = sessionManager;
    public DeskConfig Config { get; } = config;

    public List<Company> Companies { get; private set; } = [];
    public int? SelectedCompanyId => SessionManager.GetSelectedCompanyId();

    public override async Task OnPageHandlerExecutionAsync(
        PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        var apiKey = SessionManager.GetApiKey();

        // No API key → redirect to profile page (multi-user) or show error
        if (apiKey is null && !Config.IsStandalone)
        {
            context.Result = new RedirectToPageResult("/Account/Manage/Index", new { area = "Identity", apiKeyRequired = true });
            return;
        }

        if (apiKey is not null)
        {
            var cached = SessionManager.GetCompanies();
            if (cached is not null)
            {
                Companies = cached;
            }
            else
            {
                try
                {
                    const int batchSize = 200;
                    var all = new List<Company>();
                    int page = 1;
                    int total;
                    do
                    {
                        var (batch, count) = await ApiManager.List<Company>(page: page, pageSize: batchSize, sort: "name");
                        total = count;
                        if (batch is not null) all.AddRange(batch);
                        page++;
                    } while (all.Count < total);

                    Companies = all;
                    SessionManager.SetCompanies(Companies);
                }
                catch
                {
                    Companies = [];
                }
            }

            // Handle company selection from navbar dropdown
            if (context.HttpContext.Request.Query.ContainsKey("companyId"))
            {
                var raw = context.HttpContext.Request.Query["companyId"].ToString();
                var companyId = string.IsNullOrEmpty(raw) ? null : (int?)int.Parse(raw);
                SessionManager.SetSelectedCompanyId(companyId);
            }
        }

        await next();
    }

    protected string? CompanyFilter
    {
        get
        {
            var id = SelectedCompanyId;
            return id is not null ? $"company_id={id}" : null;
        }
    }
}
