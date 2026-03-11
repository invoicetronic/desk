using Desk.Data;
using Desk.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
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

    public const int PreviewDays = 15;

    public List<Company> Companies { get; private set; } = [];
    public int? SelectedCompanyId => SessionManager.GetSelectedCompanyId();
    public int? PreviewDaysLeft { get; private set; }

    public override async Task OnPageHandlerExecutionAsync(
        PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        var apiKey = SessionManager.GetApiKey();

        // Session expired but user still authenticated → reload API key from DB
        if (apiKey is null && !Config.IsStandalone)
        {
            var um = context.HttpContext.RequestServices.GetRequiredService<UserManager<DeskUser>>();
            var user = await um.GetUserAsync(context.HttpContext.User);
            if (user?.ApiKey is { Length: > 0 } savedKey)
            {
                var protector = context.HttpContext.RequestServices.GetRequiredService<ApiKeyProtector>();
                var plainKey = protector.Unprotect(savedKey);
                SessionManager.SetApiKey(plainKey);
                apiKey = plainKey;
            }
        }

        // No API key → redirect to profile page (multi-user) or show error
        if (apiKey is null && !Config.IsStandalone)
        {
            context.Result = new RedirectToPageResult("/Account/Manage/Index", new { area = "Identity", apiKeyRequired = true });
            return;
        }

        // Subscription guard: require active subscription or preview period when billing is enabled
        if (Config.IsBillingEnabled)
        {
            var um = context.HttpContext.RequestServices.GetRequiredService<UserManager<DeskUser>>();
            var user = await um.GetUserAsync(context.HttpContext.User);

            if (user?.SubscriptionStatus is "active" or "trialing")
            {
                // Paid user, no banner
            }
            else
            {
                var daysLeft = PreviewDays - (int)(DateTime.UtcNow - user!.CreatedAt).TotalDays;
                if (daysLeft > 0)
                {
                    PreviewDaysLeft = daysLeft;
                }
                else
                {
                    context.Result = new RedirectToPageResult("/Billing/Subscribe");
                    return;
                }
            }
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
