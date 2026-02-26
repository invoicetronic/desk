using System.Net;
using Desk.Models;
using Desk.Tests.Helpers;

namespace Desk.Tests;

public class ApiManagerTests
{
    private static (ApiManager manager, MockHttpMessageHandler handler) CreateManager()
    {
        var config = new DeskConfig { ApiUrl = "https://api.test.com", ApiKey = "test-key" };
        var handler = new MockHttpMessageHandler()
            .WithResponse(HttpStatusCode.OK, "[]");

        var httpClient = new HttpClient(handler);
        var httpContext = new DefaultHttpContext { Session = new TestSession() };
        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        var sessionManager = new SessionManager(accessor, config);
        var apiClient = new ApiClient(config, sessionManager, httpClient);
        var manager = new ApiManager(apiClient);

        return (manager, handler);
    }

    [Fact]
    public async Task List_BuildsCorrectQueryString_WithPaginationAndSort()
    {
        var (manager, handler) = CreateManager();
        handler.WithResponse(HttpStatusCode.OK, "[]");

        _ = await manager.List<Company>(page: 2, pageSize: 50, sort: "-created");

        var uri = handler.LastRequest!.RequestUri!.ToString();
        Assert.Contains("page=2", uri);
        Assert.Contains("page_size=50", uri);
        Assert.Contains("sort=-created", uri);
    }

    [Fact]
    public async Task List_AppendsExtraQueryParams()
    {
        var (manager, handler) = CreateManager();
        handler.WithResponse(HttpStatusCode.OK, "[]");

        _ = await manager.List<Company>(extraQuery: "company_id=5");

        var uri = handler.LastRequest!.RequestUri!.ToString();
        Assert.Contains("company_id=5", uri);
    }

    [Fact]
    public async Task Get_CallsCorrectEndpoint_ForCompany()
    {
        var (manager, handler) = CreateManager();
        handler.WithResponse(HttpStatusCode.OK, """{"id": 1, "name": "Test"}""");

        _ = await manager.Get<Company>(1);

        Assert.Contains("company/1", handler.LastRequest!.RequestUri!.ToString());
    }

    [Fact]
    public async Task Get_CallsCorrectEndpoint_ForSend()
    {
        var (manager, handler) = CreateManager();
        handler.WithResponse(HttpStatusCode.OK, """{"id": 1}""");

        _ = await manager.Get<Send>(42);

        Assert.Contains("send/42", handler.LastRequest!.RequestUri!.ToString());
    }

    [Fact]
    public async Task UploadInvoice_DelegatesToPostMultipart()
    {
        var (manager, handler) = CreateManager();
        handler.WithResponse(HttpStatusCode.OK,
            """{"id": 1, "company_id": 2, "file_name": "test.xml"}""");

        var content = new MemoryStream("<xml/>"u8.ToArray());
        var file = new FormFile(content, 0, content.Length, "file", "test.xml")
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/xml"
        };

        var result = await manager.UploadInvoice(file);

        Assert.Contains("send/file", handler.LastRequest!.RequestUri!.ToString());
        Assert.NotNull(result);
    }

    [Fact]
    public async Task Export_CallsGetBytes_WithQueryParams()
    {
        var (manager, handler) = CreateManager();
        byte[] zipBytes = [0x50, 0x4B, 0x03, 0x04];
        handler.WithBytesResponse(HttpStatusCode.OK, zipBytes);

        var result = await manager.Export("type=send&company_id=1");

        var uri = handler.LastRequest!.RequestUri!.ToString();
        Assert.Contains("export", uri);
        Assert.Contains("type=send", uri);
        Assert.Contains("company_id=1", uri);
    }

    [Fact]
    public async Task GetStatus_CallsStatusEndpoint()
    {
        var (manager, handler) = CreateManager();
        handler.WithResponse(HttpStatusCode.OK,
            """{"operation_left": 100, "signature_left": 50}""");

        var result = await manager.GetStatus();

        Assert.Contains("status", handler.LastRequest!.RequestUri!.ToString());
        Assert.NotNull(result);
        Assert.Equal(100, result!.OperationLeft);
    }
}
