using System.Net;
using System.Text;

namespace Desk.Tests.Helpers;

/// <summary>
/// Like <see cref="IntegrationApiHandler"/>, but records the Basic auth user name
/// of every call so tests can assert that requests are actually authenticated.
/// </summary>
public class RecordingApiHandler(bool hasActiveSeat) : HttpMessageHandler
{
    private readonly List<(string Path, string? User)> _calls = [];

    public IReadOnlyList<(string Path, string? User)> Calls
    {
        get { lock (_calls) return _calls.ToList(); }
    }

    public void ClearCalls()
    {
        lock (_calls) _calls.Clear();
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri?.AbsolutePath.TrimStart('/') ?? "";
        var parameter = request.Headers.Authorization?.Parameter;
        var user = parameter is null
            ? null
            : Encoding.UTF8.GetString(Convert.FromBase64String(parameter)).Split(':')[0];

        lock (_calls) _calls.Add((path, user));

        var json = path == "status"
            ? $$"""{"operation_left":0,"signature_left":0,"has_active_seat":{{(hasActiveSeat ? "true" : "false")}}}"""
            : "[]";

        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        response.Headers.TryAddWithoutValidation("Invoicetronic-Total-Count", "0");

        return Task.FromResult(response);
    }
}
