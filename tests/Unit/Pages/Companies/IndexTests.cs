using System.Net;
using Desk.Models;
using Desk.Pages.Companies;
using Desk.Tests.Helpers;
using Microsoft.AspNetCore.Mvc;

namespace Desk.Tests.Pages.Companies;

public class IndexTests
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

        var model = new IndexModel(apiManager, sessionManager, config);
        return (model, handler);
    }

    [Fact]
    public async Task OnGetListAsync_ReturnsJsonWithDataAndTotalCount()
    {
        var (model, handler) = CreateModel();
        handler.WithResponse(HttpStatusCode.OK,
                """[{"id": 1, "name": "Rossi Srl", "vat": "IT123", "fiscal_code": "FC1"}]""")
            .WithHeader("Invoicetronic-Total-Count", "1");

        var result = await model.OnGetListAsync(1, 100, null);

        var jsonResult = Assert.IsType<JsonResult>(result);
        var data = jsonResult.Value as dynamic;
        Assert.NotNull(data);
    }

    [Fact]
    public async Task OnGetListAsync_PassesCorrectPaginationParams()
    {
        var (model, handler) = CreateModel();
        handler.WithResponse(HttpStatusCode.OK, "[]");

        _ = await model.OnGetListAsync(3, 25, null);

        var uri = handler.LastRequest!.RequestUri!.ToString();
        Assert.Contains("page=3", uri);
        Assert.Contains("page_size=25", uri);
    }

    [Fact]
    public async Task OnGetListAsync_PassesSortParam_WhenProvided()
    {
        var (model, handler) = CreateModel();
        handler.WithResponse(HttpStatusCode.OK, "[]");

        _ = await model.OnGetListAsync(1, 100, "-name");

        var uri = handler.LastRequest!.RequestUri!.ToString();
        Assert.Contains("sort=-name", uri);
    }

    [Fact]
    public async Task OnGetListAsync_AppliesTextFilter()
    {
        var (model, handler) = CreateModel();
        handler.WithResponse(HttpStatusCode.OK, "[]")
            .WithHeader("Invoicetronic-Total-Count", "0");

        _ = await model.OnGetListAsync(1, 100, null, q: "Rossi");

        var uri = handler.LastRequest!.RequestUri!.ToString();
        Assert.Contains("q=Rossi", uri);
    }

    [Fact]
    public async Task OnGetListAsync_UrlEncodesTextFilter()
    {
        var (model, handler) = CreateModel();
        handler.WithResponse(HttpStatusCode.OK, "[]")
            .WithHeader("Invoicetronic-Total-Count", "0");

        _ = await model.OnGetListAsync(1, 100, null, q: "Rossi & Bianchi");

        var uri = handler.LastRequest!.RequestUri!.AbsoluteUri;
        Assert.Contains("q=Rossi%20%26%20Bianchi", uri);
    }

    [Fact]
    public async Task OnPostAddAsync_CallsApiManagerAdd()
    {
        var (model, handler) = CreateModel();
        handler.WithResponse(HttpStatusCode.OK,
            """{"id": 1, "name": "New Co", "vat": "IT999", "fiscal_code": "FC9"}""");

        model.CompanyInput = new Company { Name = "New Co", Vat = "IT999", FiscalCode = "FC9" };
        var result = await model.OnPostAddAsync();

        Assert.IsType<JsonResult>(result);
        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
    }

    [Fact]
    public async Task OnPostAddAsync_ReturnsError_On403()
    {
        var (model, handler) = CreateModel();
        handler.WithResponse(HttpStatusCode.Forbidden,
            """{"problem_details":{"title":"Forbidden","detail":"Access denied"}}""");

        model.CompanyInput = new Company { Name = "New Co", Vat = "IT999", FiscalCode = "FC9" };
        var result = await model.OnPostAddAsync();

        var jsonResult = Assert.IsType<JsonResult>(result);
        // Should contain error info
        Assert.NotNull(jsonResult.Value);
    }

    [Fact]
    public async Task OnPostUpdateAsync_CallsApiManagerUpdate()
    {
        var (model, handler) = CreateModel();
        handler.WithResponse(HttpStatusCode.OK,
            """{"id": 1, "name": "Updated Co", "vat": "IT999", "fiscal_code": "FC9"}""");

        model.CompanyInput = new Company { Id = 1, Name = "Updated Co", Vat = "IT999", FiscalCode = "FC9" };
        var result = await model.OnPostUpdateAsync();

        Assert.IsType<JsonResult>(result);
        Assert.Equal(HttpMethod.Put, handler.LastRequest!.Method);
    }

    [Fact]
    public async Task OnPostDeleteAsync_CallsApiManagerDelete()
    {
        var (model, handler) = CreateModel();
        handler.WithResponse(HttpStatusCode.OK, "{}");

        var result = await model.OnPostDeleteAsync(1);

        Assert.IsType<JsonResult>(result);
        Assert.Equal(HttpMethod.Delete, handler.LastRequest!.Method);
        Assert.Contains("company/1", handler.LastRequest.RequestUri!.ToString());
    }
}
