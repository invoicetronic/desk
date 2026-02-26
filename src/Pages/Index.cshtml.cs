using Desk.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Desk.Pages;

public class IndexModel(ApiManager apiManager, SessionManager sessionManager, DeskConfig config)
    : AppPageModel(apiManager, sessionManager, config)
{
    public Status? AccountStatus { get; set; }
    public int SentCount { get; set; }
    public int ReceivedCount { get; set; }
    public int UnreadCount { get; set; }
    public List<Send> RecentSent { get; set; } = [];
    public List<Receive> RecentReceived { get; set; } = [];
    public List<Update> RecentUpdates { get; set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        // Load all dashboard data in parallel, tolerating failures
        var statusTask = SafeCall(() => ApiManager.GetStatus());
        var sentTask = SafeCall(() => ApiManager.List<Send>(pageSize: 5, sort: "-created", extraQuery: CompanyFilter));
        var receivedTask = SafeCall(() => ApiManager.List<Receive>(pageSize: 5, sort: "-created", extraQuery: CompanyFilter));
        var unreadTask = SafeCall(() => ApiManager.List<Receive>(pageSize: 1, extraQuery: CombineFilters(CompanyFilter, "is_read=false")));
        var updatesTask = SafeCall(() => ApiManager.List<Update>(pageSize: 10, sort: "-last_update", extraQuery: CompanyFilter));

        await Task.WhenAll(statusTask, sentTask, receivedTask, unreadTask, updatesTask);

        AccountStatus = statusTask.Result;
        if (sentTask.Result is var (sentList, sentTotal))
        {
            RecentSent = sentList ?? [];
            SentCount = sentTotal;
        }
        if (receivedTask.Result is var (recList, recTotal))
        {
            RecentReceived = recList ?? [];
            ReceivedCount = recTotal;
        }
        if (unreadTask.Result is var (_, unreadTotal))
        {
            UnreadCount = unreadTotal;
        }
        if (updatesTask.Result is var (updateList, _))
        {
            RecentUpdates = updateList ?? [];
        }

        return Page();
    }

    private static async Task<T?> SafeCall<T>(Func<Task<T>> action)
    {
        try
        {
            return await action();
        }
        catch
        {
            return default;
        }
    }

    private static string? CombineFilters(params string?[] filters)
    {
        var parts = filters.Where(f => f is not null).ToList();
        return parts.Count > 0 ? string.Join("&", parts) : null;
    }
}
