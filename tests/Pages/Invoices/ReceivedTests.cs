using System.Net;
using Desk.Models;
using Desk.Pages.Invoices;
using Desk.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Desk.Tests.Pages.Invoices;

public class ReceivedTests
{
    private static (ReceivedModel model, MockHttpMessageHandler handler) CreateModel(
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

        var model = new ReceivedModel(apiManager, sessionManager, config);
        return (model, handler);
    }

    [Fact]
    public async Task OnGetListAsync_AppliesCompanyFilter()
    {
        var (model, handler) = CreateModel(selectedCompanyId: 42);
        handler.WithResponse(HttpStatusCode.OK, "[]")
            .WithHeader("Invoicetronic-Total-Count", "0");

        _ = await model.OnGetListAsync(1, 50, null);

        var uri = handler.LastRequest!.RequestUri!.ToString();
        Assert.Contains("company_id=42", uri);
    }

    [Fact]
    public async Task OnGetListAsync_AppliesUnreadFilter()
    {
        var (model, handler) = CreateModel();
        handler.WithResponse(HttpStatusCode.OK, "[]")
            .WithHeader("Invoicetronic-Total-Count", "0");

        _ = await model.OnGetListAsync(1, 50, null, unreadOnly: true);

        var uri = handler.LastRequest!.RequestUri!.ToString();
        Assert.Contains("unread=true", uri);
    }

    [Fact]
    public async Task OnGetListAsync_AppliesTextFilter()
    {
        var (model, handler) = CreateModel();
        handler.WithResponse(HttpStatusCode.OK, "[]")
            .WithHeader("Invoicetronic-Total-Count", "0");

        _ = await model.OnGetListAsync(1, 50, null, q: "Bianchi");

        var uri = handler.LastRequest!.RequestUri!.ToString();
        Assert.Contains("q=Bianchi", uri);
    }

    [Fact]
    public async Task OnGetListAsync_UrlEncodesTextFilter()
    {
        var (model, handler) = CreateModel();
        handler.WithResponse(HttpStatusCode.OK, "[]")
            .WithHeader("Invoicetronic-Total-Count", "0");

        _ = await model.OnGetListAsync(1, 50, null, q: "Rossi & Bianchi");

        var uri = handler.LastRequest!.RequestUri!.AbsoluteUri;
        Assert.Contains("q=Rossi%20%26%20Bianchi", uri);
    }

    [Fact]
    public async Task OnGetListAsync_CombinesFilters()
    {
        var (model, handler) = CreateModel(selectedCompanyId: 5);
        handler.WithResponse(HttpStatusCode.OK, "[]")
            .WithHeader("Invoicetronic-Total-Count", "0");

        _ = await model.OnGetListAsync(1, 50, "-created",
            unreadOnly: true, q: "test");

        var uri = handler.LastRequest!.RequestUri!.ToString();
        Assert.Contains("company_id=5", uri);
        Assert.Contains("unread=true", uri);
        Assert.Contains("q=test", uri);
        Assert.Contains("sort=-created", uri);
    }

    [Fact]
    public async Task OnGetListAsync_ReturnsJsonResult()
    {
        var (model, handler) = CreateModel();
        handler.WithResponse(HttpStatusCode.OK,
                """[{"id":1,"file_name":"rx.xml","committente":"Buyer","prestatore":"Seller","format":"FPA12","is_read":false,"message_id":"MSG1"}]""")
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
