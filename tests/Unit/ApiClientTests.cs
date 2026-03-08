using System.Net;
using Desk.Tests.Helpers;

namespace Desk.Tests;

public class ApiClientTests
{
    private static (ApiClient client, MockHttpMessageHandler handler) CreateClient(string apiKey = "test-key")
    {
        var config = new DeskConfig { ApiUrl = "https://api.test.com" };
        var handler = new MockHttpMessageHandler()
            .WithResponse(HttpStatusCode.OK, "{}");

        var httpClient = new HttpClient(handler);
        var sessionConfig = new DeskConfig { ApiKey = apiKey };
        var httpContext = new DefaultHttpContext { Session = new TestSession() };
        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        var sessionManager = new SessionManager(accessor, sessionConfig);

        var client = new ApiClient(config, sessionManager, httpClient);
        return (client, handler);
    }

    [Fact]
    public async Task Get_SetsBasicAuthHeader_WithApiKey()
    {
        var (client, handler) = CreateClient("my-api-key");
        handler.WithResponse(HttpStatusCode.OK, """{"id": 1}""");

        _ = await client.Get<object>("test");

        Assert.NotNull(handler.LastRequest);
        var authHeader = handler.LastRequest!.Headers.Authorization;
        Assert.Equal("Basic", authHeader!.Scheme);

        var decoded = System.Text.Encoding.UTF8.GetString(
            Convert.FromBase64String(authHeader.Parameter!));
        Assert.Equal("my-api-key:", decoded);
    }

    [Fact]
    public async Task Get_UsesSnakeCaseJsonDeserialization()
    {
        var (client, handler) = CreateClient();
        handler.WithResponse(HttpStatusCode.OK,
            """{"operation_left": 42, "signature_left": 10}""");

        var result = await client.Get<Models.Status>("status");

        Assert.NotNull(result);
        Assert.Equal(42, result!.OperationLeft);
        Assert.Equal(10, result.SignatureLeft);
    }

    [Fact]
    public async Task List_ParsesTotalCountHeader()
    {
        var (client, handler) = CreateClient();
        handler.WithResponse(HttpStatusCode.OK, "[]")
               .WithHeader("Invoicetronic-Total-Count", "150");

        var (_, totalCount) = await client.List<Models.Company>("company");

        Assert.Equal(150, totalCount);
    }

    [Fact]
    public async Task List_ReturnsEmptyList_WhenNoData()
    {
        var (client, handler) = CreateClient();
        handler.WithResponse(HttpStatusCode.OK, "[]");

        var (entity, totalCount) = await client.List<Models.Company>("company");

        Assert.NotNull(entity);
        Assert.Empty(entity!);
        Assert.Equal(0, totalCount);
    }

    [Fact]
    public async Task Post_SerializesBodyAsSnakeCaseJson()
    {
        var (client, handler) = CreateClient();
        handler.WithResponse(HttpStatusCode.OK, """{"id": 1}""");

        var company = new Models.Company { FiscalCode = "ABC123", Name = "Test" };
        _ = await client.Post<Models.Company>("company", company);

        Assert.Contains("fiscal_code", handler.LastRequestContent);
        Assert.Contains("ABC123", handler.LastRequestContent!);
    }

    [Fact]
    public void ExtractErrorMessage_ParsesProblemDetails()
    {
        var errorBody = """
        {
            "problem_details": {
                "title": "Bad Request",
                "detail": "VAT number is invalid"
            }
        }
        """;

        var message = ApiClient.ExtractErrorMessage(errorBody);

        Assert.Equal("VAT number is invalid", message);
    }

    [Fact]
    public async Task GetBytes_ReturnsByteArray_ForBinaryResponse()
    {
        var (client, handler) = CreateClient();
        byte[] expected = [0x50, 0x4B, 0x03, 0x04];
        handler.WithBytesResponse(HttpStatusCode.OK, expected);

        var result = await client.GetBytes("export?type=send");

        Assert.NotNull(result);
        Assert.Equal(expected, result);
    }
}
