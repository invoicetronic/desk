using System.Net;
using Desk.Models;
using Desk.Pages;
using Desk.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging.Abstractions;

namespace Desk.Tests.Pages;

public class HomeTests
{
    private static (IndexModel model, MockHttpMessageHandler handler) CreateModel(
        string apiKey = "test-key", int? selectedCompanyId = null)
    {
        var config = new DeskConfig { ApiUrl = "https://api.test.com", ApiKey = apiKey };
        var handler = new MockHttpMessageHandler()
            .WithResponse(HttpStatusCode.OK, "[]");
        var httpClient = new HttpClient(handler);
        var httpContext = new DefaultHttpContext { Session = new TestSession() };
        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        var sessionManager = new SessionManager(accessor, config);
        var apiClient = new ApiClient(config, sessionManager, httpClient);
        var apiManager = new ApiManager(apiClient);

        if (selectedCompanyId is not null)
            sessionManager.SetSelectedCompanyId(selectedCompanyId);

        var model = new IndexModel(apiManager, sessionManager, config, NullLogger<IndexModel>.Instance);
        return (model, handler);
    }

    [Fact]
    public async Task OnGetAsync_ReturnsPage_WhenApiResponds()
    {
        var (model, handler) = CreateModel();
        // Mock responds with empty data for all calls
        handler.WithResponse(HttpStatusCode.OK, "[]")
            .WithHeader("Invoicetronic-Total-Count", "0");

        var result = await model.OnGetAsync();

        Assert.IsType<PageResult>(result);
    }

    [Fact]
    public async Task OnGetAsync_HandlesApiDown_Gracefully()
    {
        var (model, handler) = CreateModel();
        // API returns error HTML
        handler.WithResponse(HttpStatusCode.InternalServerError,
            """{"problem_details":{"title":"Error"}}""");

        var result = await model.OnGetAsync();

        // Should still render the page, not throw
        Assert.IsType<PageResult>(result);
        Assert.Equal(0, model.SentCount);
        Assert.Equal(0, model.ReceivedCount);
        Assert.Equal(0, model.UnreadCount);
    }

    [Fact]
    public async Task OnGetAsync_ReturnsZeroCounts_WhenNoData()
    {
        var (model, handler) = CreateModel();
        handler.WithResponse(HttpStatusCode.OK, "[]")
            .WithHeader("Invoicetronic-Total-Count", "0");

        _ = await model.OnGetAsync();

        Assert.Equal(0, model.SentCount);
        Assert.Equal(0, model.ReceivedCount);
        Assert.Equal(0, model.UnreadCount);
    }
}
