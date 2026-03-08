using System.Net;
using Desk.Models;
using Desk.Pages.Export;
using Desk.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Desk.Tests.Pages.Export;

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
    public async Task OnGetDownloadAsync_BuildsCorrectQueryString_WithDateRange()
    {
        var (model, handler) = CreateModel();
        handler.WithBytesResponse(HttpStatusCode.OK, [0x50, 0x4B, 0x03, 0x04]);

        _ = await model.OnGetDownloadAsync("send", "2025-01-01", "2025-12-31");

        var uri = handler.LastRequest!.RequestUri!.ToString();
        Assert.Contains("type=send", uri);
        Assert.Contains("document_date_from=2025-01-01", uri);
        Assert.Contains("document_date_to=2025-12-31", uri);
    }

    [Fact]
    public async Task OnGetDownloadAsync_BuildsCorrectQueryString_WithMonth()
    {
        var (model, handler) = CreateModel();
        handler.WithBytesResponse(HttpStatusCode.OK, [0x50, 0x4B, 0x03, 0x04]);

        _ = await model.OnGetDownloadAsync("send", year: 2025, month: 6);

        var uri = handler.LastRequest!.RequestUri!.ToString();
        Assert.Contains("type=send", uri);
        Assert.Contains("year=2025", uri);
        Assert.Contains("month=6", uri);
        Assert.DoesNotContain("document_date_from", uri);
        Assert.DoesNotContain("quarter", uri);
    }

    [Fact]
    public async Task OnGetDownloadAsync_BuildsCorrectQueryString_WithQuarter()
    {
        var (model, handler) = CreateModel();
        handler.WithBytesResponse(HttpStatusCode.OK, [0x50, 0x4B, 0x03, 0x04]);

        _ = await model.OnGetDownloadAsync("receive", year: 2025, quarter: 3);

        var uri = handler.LastRequest!.RequestUri!.ToString();
        Assert.Contains("type=receive", uri);
        Assert.Contains("year=2025", uri);
        Assert.Contains("quarter=3", uri);
        Assert.DoesNotContain("document_date_from", uri);
        Assert.DoesNotContain("month", uri);
    }

    [Fact]
    public async Task OnGetDownloadAsync_IncludesCompanyId_WhenSelected()
    {
        var (model, handler) = CreateModel(selectedCompanyId: 42);
        handler.WithBytesResponse(HttpStatusCode.OK, [0x50, 0x4B, 0x03, 0x04]);

        _ = await model.OnGetDownloadAsync("send", "2025-01-01", "2025-12-31");

        var uri = handler.LastRequest!.RequestUri!.ToString();
        Assert.Contains("company_id=42", uri);
    }

    [Fact]
    public async Task OnGetDownloadAsync_OmitsCompanyId_WhenAllSelected()
    {
        var (model, handler) = CreateModel(selectedCompanyId: null);
        handler.WithBytesResponse(HttpStatusCode.OK, [0x50, 0x4B, 0x03, 0x04]);

        _ = await model.OnGetDownloadAsync("send", "2025-01-01", "2025-12-31");

        var uri = handler.LastRequest!.RequestUri!.ToString();
        Assert.DoesNotContain("company_id", uri);
    }

    [Fact]
    public async Task OnGetDownloadAsync_ReturnsZipFile()
    {
        var zipBytes = new byte[] { 0x50, 0x4B, 0x03, 0x04, 0x00, 0x00 };
        var (model, handler) = CreateModel();
        handler.WithBytesResponse(HttpStatusCode.OK, zipBytes);

        _ = await model.OnGetDownloadAsync("both", "2025-01-01", "2025-12-31");

        var fileResult = Assert.IsType<FileContentResult>(await model.OnGetDownloadAsync("both", year: 2025, month: 1));
        Assert.Equal("application/zip", fileResult.ContentType);
        Assert.EndsWith(".zip", fileResult.FileDownloadName);
    }

    [Fact]
    public async Task OnGetDownloadAsync_ReturnsError_OnApiFailure()
    {
        var (model, handler) = CreateModel();
        handler.WithResponse(HttpStatusCode.InternalServerError,
            """{"problem_details":{"title":"Error"}}""");

        var result = await model.OnGetDownloadAsync("send", "2025-01-01", "2025-12-31");

        var jsonResult = Assert.IsType<JsonResult>(result);
        Assert.Equal(500, jsonResult.StatusCode);
    }
}
