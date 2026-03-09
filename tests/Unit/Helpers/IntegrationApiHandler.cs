using System.Net;
using System.Text;

namespace Desk.Tests.Helpers;

public class IntegrationApiHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri?.AbsolutePath.TrimStart('/') ?? "";

        var (json, totalCount) = path switch
        {
            "status" => ("""{"operation_left":0,"signature_left":0}""", 0),
            _ => ("[]", 0)
        };

        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        response.Headers.TryAddWithoutValidation("Invoicetronic-Total-Count", totalCount.ToString());

        return Task.FromResult(response);
    }
}
