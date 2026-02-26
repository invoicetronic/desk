using System.Net;
using System.Net.Http.Headers;

namespace Desk.Tests.Helpers;

public class MockHttpMessageHandler : HttpMessageHandler
{
    private HttpStatusCode _statusCode = HttpStatusCode.OK;
    private string _responseContent = "";
    private readonly Dictionary<string, string> _responseHeaders = new();
    private byte[]? _responseBytes;

    public HttpRequestMessage? LastRequest { get; private set; }
    public string? LastRequestContent { get; private set; }
    public List<HttpRequestMessage> AllRequests { get; } = [];

    public MockHttpMessageHandler WithResponse(HttpStatusCode statusCode, string content)
    {
        _statusCode = statusCode;
        _responseContent = content;
        return this;
    }

    public MockHttpMessageHandler WithHeader(string name, string value)
    {
        _responseHeaders[name] = value;
        return this;
    }

    public MockHttpMessageHandler WithBytesResponse(HttpStatusCode statusCode, byte[] bytes)
    {
        _statusCode = statusCode;
        _responseBytes = bytes;
        return this;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        AllRequests.Add(request);

        if (request.Content is not null)
            LastRequestContent = await request.Content.ReadAsStringAsync(cancellationToken);

        HttpContent content = _responseBytes is not null
            ? new ByteArrayContent(_responseBytes)
            : new StringContent(_responseContent);

        var response = new HttpResponseMessage(_statusCode) { Content = content };

        foreach (var (name, value) in _responseHeaders)
            response.Headers.TryAddWithoutValidation(name, value);

        return response;
    }
}
