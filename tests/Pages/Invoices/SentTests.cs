using System.Net;
using Desk.Models;
using Desk.Pages.Invoices;
using Desk.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Desk.Tests.Pages.Invoices;

public class SentTests
{
    private static (SentModel model, MockHttpMessageHandler handler) CreateModel(
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

        var model = new SentModel(apiManager, sessionManager, config);
        return (model, handler);
    }

    private static string GetSendRequestUri(MockHttpMessageHandler handler)
    {
        return handler.AllRequests
            .Select(r => r.RequestUri!.ToString())
            .First(u => u.Contains("/send"));
    }

    [Fact]
    public async Task OnGetListAsync_AppliesCompanyFilter()
    {
        var (model, handler) = CreateModel(selectedCompanyId: 42);
        handler.WithResponse(HttpStatusCode.OK, "[]")
            .WithHeader("Invoicetronic-Total-Count", "0");

        _ = await model.OnGetListAsync(1, 50, null);

        var uri = GetSendRequestUri(handler);
        Assert.Contains("company_id=42", uri);
    }

    [Fact]
    public async Task OnGetListAsync_AppliesDateRangeFilters()
    {
        var (model, handler) = CreateModel();
        handler.WithResponse(HttpStatusCode.OK, "[]")
            .WithHeader("Invoicetronic-Total-Count", "0");

        _ = await model.OnGetListAsync(1, 50, null,
            dateFrom: "2025-01-01", dateTo: "2025-12-31");

        var uri = GetSendRequestUri(handler);
        Assert.Contains("date_sent_from=2025-01-01T00:00:00Z", uri);
        Assert.Contains("date_sent_to=2025-12-31T23:59:59Z", uri);
    }

    [Fact]
    public async Task OnGetListAsync_AppliesTextFilters()
    {
        var (model, handler) = CreateModel();
        handler.WithResponse(HttpStatusCode.OK, "[]")
            .WithHeader("Invoicetronic-Total-Count", "0");

        _ = await model.OnGetListAsync(1, 50, null, q: "Rossi");

        var uri = GetSendRequestUri(handler);
        Assert.Contains("q=Rossi", uri);
    }

    [Fact]
    public async Task OnGetListAsync_UrlEncodesTextFilter()
    {
        var (model, handler) = CreateModel();
        handler.WithResponse(HttpStatusCode.OK, "[]")
            .WithHeader("Invoicetronic-Total-Count", "0");

        _ = await model.OnGetListAsync(1, 50, null, q: "Rossi & Bianchi");

        var uri = handler.AllRequests
            .Select(r => r.RequestUri!.AbsoluteUri)
            .First(u => u.Contains("/send"));
        Assert.Contains("q=Rossi%20%26%20Bianchi", uri);
    }

    [Fact]
    public async Task OnGetListAsync_CombinesMultipleFilters()
    {
        var (model, handler) = CreateModel(selectedCompanyId: 10);
        handler.WithResponse(HttpStatusCode.OK, "[]")
            .WithHeader("Invoicetronic-Total-Count", "0");

        _ = await model.OnGetListAsync(1, 50, "-date_sent",
            dateFrom: "2025-06-01", q: "test");

        var uri = GetSendRequestUri(handler);
        Assert.Contains("company_id=10", uri);
        Assert.Contains("date_sent_from=2025-06-01T00:00:00Z", uri);
        Assert.Contains("q=test", uri);
        Assert.Contains("sort=-date_sent", uri);
    }

    [Fact]
    public async Task OnGetListAsync_ReturnsJsonResult()
    {
        var (model, handler) = CreateModel();
        handler.WithResponse(HttpStatusCode.OK,
                """[{"id":1,"identifier":"IT01","file_name":"test.xml","committente":"IT01234567890","nome_committente":"Buyer Srl","prestatore":"Seller","format":"FPA12","date_sent":"2025-01-01"}]""")
            .WithHeader("Invoicetronic-Total-Count", "1");

        var result = await model.OnGetListAsync(1, 50, null);

        var jsonResult = Assert.IsType<JsonResult>(result);
        Assert.NotNull(jsonResult.Value);
    }

    [Fact]
    public async Task OnGetListAsync_ReturnsError_OnApiFailure()
    {
        var (model, handler) = CreateModel();
        handler.WithResponse(HttpStatusCode.InternalServerError,
            """{"problem_details":{"title":"Internal Server Error"}}""");

        var result = await model.OnGetListAsync(1, 50, null);

        var jsonResult = Assert.IsType<JsonResult>(result);
        Assert.Equal(500, jsonResult.StatusCode);
    }
}
