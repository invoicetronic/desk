using System.Net;
using Desk.Models;
using Desk.Pages.Upload;
using Desk.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Desk.Tests.Pages.Upload;

public class IndexTests
{
    private static (IndexModel model, MockHttpMessageHandler handler) CreateModel(
        string apiKey = "test-key")
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

        var model = new IndexModel(apiManager, sessionManager, config);
        return (model, handler);
    }

    private static IFormFile CreateFakeFile(string fileName, string content = "<xml/>", long? overrideLength = null)
    {
        var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
        return new FormFile(stream, 0, overrideLength ?? stream.Length, "file", fileName);
    }

    [Fact]
    public async Task OnPostUploadAsync_RejectsInvalidExtension()
    {
        var (model, _) = CreateModel();
        var file = CreateFakeFile("test.pdf");

        var result = await model.OnPostUploadAsync(file);

        var jsonResult = Assert.IsType<JsonResult>(result);
        Assert.Equal(400, jsonResult.StatusCode);
    }

    [Fact]
    public async Task OnPostUploadAsync_RejectsOversizedFile()
    {
        var (model, _) = CreateModel();
        var file = CreateFakeFile("test.xml", overrideLength: 6 * 1024 * 1024); // 6MB

        var result = await model.OnPostUploadAsync(file);

        var jsonResult = Assert.IsType<JsonResult>(result);
        Assert.Equal(400, jsonResult.StatusCode);
    }

    [Fact]
    public async Task OnPostUploadAsync_RejectsEmptyFile()
    {
        var (model, _) = CreateModel();
        var file = CreateFakeFile("test.xml", "");

        var result = await model.OnPostUploadAsync(file);

        var jsonResult = Assert.IsType<JsonResult>(result);
        Assert.Equal(400, jsonResult.StatusCode);
    }

    [Fact]
    public async Task OnPostUploadAsync_AcceptsXmlFile()
    {
        var (model, handler) = CreateModel();
        handler.WithResponse(HttpStatusCode.OK,
            """{"id":1,"company_id":10,"identifier":"IT01","file_name":"test.xml"}""");
        var file = CreateFakeFile("test.xml");

        var result = await model.OnPostUploadAsync(file);

        var jsonResult = Assert.IsType<JsonResult>(result);
        Assert.Null(jsonResult.StatusCode); // 200 (default)
    }

    [Fact]
    public async Task OnPostUploadAsync_AcceptsP7mFile()
    {
        var (model, handler) = CreateModel();
        handler.WithResponse(HttpStatusCode.OK,
            """{"id":2,"company_id":10,"identifier":"IT02","file_name":"test.xml.p7m"}""");
        var file = CreateFakeFile("test.xml.p7m");

        var result = await model.OnPostUploadAsync(file);

        var jsonResult = Assert.IsType<JsonResult>(result);
        Assert.Null(jsonResult.StatusCode);
    }

    [Fact]
    public async Task OnPostUploadAsync_ReturnsApiError_OnFailure()
    {
        var (model, handler) = CreateModel();
        handler.WithResponse(HttpStatusCode.BadRequest,
            """{"problem_details":{"title":"Bad Request","detail":"Invalid XML"}}""");
        var file = CreateFakeFile("test.xml");

        var result = await model.OnPostUploadAsync(file);

        var jsonResult = Assert.IsType<JsonResult>(result);
        Assert.NotNull(jsonResult.StatusCode);
        Assert.True(jsonResult.StatusCode >= 400);
    }

    [Fact]
    public async Task OnPostUploadAsync_ReturnsSuccess_WithData()
    {
        var (model, handler) = CreateModel();
        handler.WithResponse(HttpStatusCode.OK,
            """{"id":5,"company_id":10,"identifier":"IT05","file_name":"invoice.xml"}""");
        var file = CreateFakeFile("invoice.xml");

        var result = await model.OnPostUploadAsync(file);

        var jsonResult = Assert.IsType<JsonResult>(result);
        Assert.Null(jsonResult.StatusCode);
    }
}
