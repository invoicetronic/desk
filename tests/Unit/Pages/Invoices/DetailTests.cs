using System.Net;
using Desk.Models;
using Desk.Pages.Invoices;
using Desk.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging.Abstractions;

namespace Desk.Tests.Pages.Invoices;

public class DetailTests
{
    private static (DetailModel model, MockHttpMessageHandler handler) CreateModel(
        string apiKey = "test-key")
    {
        var config = new DeskConfig { ApiUrl = "https://api.test.com", ApiKey = apiKey };
        var handler = new MockHttpMessageHandler()
            .WithResponse(HttpStatusCode.OK, "{}");
        var httpClient = new HttpClient(handler);
        var httpContext = new DefaultHttpContext { Session = new TestSession() };
        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        var sessionManager = new SessionManager(accessor, config);
        var apiClient = new ApiClient(config, sessionManager, httpClient);
        var apiManager = new ApiManager(apiClient);

        var model = new DetailModel(apiManager, sessionManager, config, NullLogger<DetailModel>.Instance);
        return (model, handler);
    }

    [Fact]
    public async Task OnGetAsync_LoadsSendInvoice_WhenTypeSend()
    {
        var (model, handler) = CreateModel();
        handler.WithResponse(HttpStatusCode.OK,
            """{"id":1,"identifier":"IT01","file_name":"test.xml","committente":"Buyer","prestatore":"Seller","format":"FPA12"}""");

        var result = await model.OnGetAsync("send", 1);

        Assert.IsType<PageResult>(result);
        Assert.NotNull(model.SendInvoice);
        Assert.Equal(1, model.SendInvoice.Id);
        Assert.Null(model.ReceiveInvoice);
        Assert.Equal("send", model.InvoiceType);
    }

    [Fact]
    public async Task OnGetAsync_LoadsReceiveInvoice_WhenTypeReceive()
    {
        var (model, handler) = CreateModel();
        handler.WithResponse(HttpStatusCode.OK,
            """{"id":2,"file_name":"rx.xml","committente":"Buyer","prestatore":"Seller","format":"FPA12","is_read":true,"message_id":"MSG1"}""");

        var result = await model.OnGetAsync("receive", 2);

        Assert.IsType<PageResult>(result);
        Assert.NotNull(model.ReceiveInvoice);
        Assert.Null(model.SendInvoice);
        Assert.Contains("receive/2", handler.LastRequest!.RequestUri!.ToString());
    }

    [Fact]
    public async Task OnGetAsync_ReturnsNotFound_WhenInvoiceNotExists()
    {
        var (model, handler) = CreateModel();
        handler.WithResponse(HttpStatusCode.NotFound,
            """{"problem_details":{"title":"Not Found"}}""");

        var result = await model.OnGetAsync("send", 999);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task OnGetAsync_ReturnsNotFound_WhenInvalidType()
    {
        var (model, _) = CreateModel();

        var result = await model.OnGetAsync("invalid", 1);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task OnGetAsync_LoadsUpdates_ForSendInvoice()
    {
        var (model, handler) = CreateModel();

        // Mock returns same response for both calls — invoice GET and updates LIST
        // The updates LIST will fail to deserialize as Update list, but catch block handles it
        handler.WithResponse(HttpStatusCode.OK,
            """{"id":1,"identifier":"IT01","file_name":"test.xml","committente":"Buyer","prestatore":"Seller","format":"FPA12"}""");

        var result = await model.OnGetAsync("send", 1);

        // For send invoices, Updates should be loaded (may be empty due to mock deserialization)
        Assert.IsType<PageResult>(result);
        Assert.NotNull(model.Updates);
    }

    [Fact]
    public async Task OnGetAsync_DoesNotLoadUpdates_ForReceiveInvoice()
    {
        var (model, handler) = CreateModel();
        handler.WithResponse(HttpStatusCode.OK,
            """{"id":2,"file_name":"rx.xml","committente":"Buyer","prestatore":"Seller","format":"FPA12","is_read":true,"message_id":"MSG1"}""");

        var result = await model.OnGetAsync("receive", 2);

        Assert.IsType<PageResult>(result);
        Assert.Null(model.Updates);
    }
}
