using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Desk.E2E;

/// <summary>
/// Intercepts HttpClient requests to the Invoicetronic API and returns
/// static JSON fixture data. Used by E2E tests to avoid external dependencies.
/// </summary>
public class FixtureApiHandler : HttpMessageHandler
{
    private static readonly string FixturesDir = Path.Combine(AppContext.BaseDirectory, "Fixtures");

    private static readonly Dictionary<string, string> FileCache = new();

    private static string LoadFixture(string name)
    {
        if (FileCache.TryGetValue(name, out var cached))
            return cached;

        var path = Path.Combine(FixturesDir, name);
        var content = File.ReadAllText(path);
        FileCache[name] = content;
        return content;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.AbsolutePath.TrimStart('/');
        var query = request.RequestUri.Query;

        // Export endpoint returns binary ZIP data
        if (path == "export")
        {
            var zipResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent([0x50, 0x4B, 0x03, 0x04]) // ZIP magic bytes
            };
            zipResponse.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/zip");
            zipResponse.Content.Headers.ContentDisposition =
                new System.Net.Http.Headers.ContentDispositionHeaderValue("attachment") { FileName = "export_both_20250301_120000.zip" };
            return Task.FromResult(zipResponse);
        }

        var (json, totalCount) = Route(path, query, request.Method);

        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        if (totalCount > 0)
            response.Headers.TryAddWithoutValidation("Invoicetronic-Total-Count", totalCount.ToString());

        return Task.FromResult(response);
    }

    private static (string json, int totalCount) Route(string path, string query, HttpMethod method)
    {
        // GET /status
        if (path == "status")
            return (LoadFixture("status.json"), 0);

        // POST /company (add) or PUT /company (update)
        if (path == "company" && method == HttpMethod.Post)
            return ("""{"id":3,"created":"2025-03-01T10:00:00Z","version":1,"user_id":1,"vat":"IT11223344556","fiscal_code":"11223344556","name":"New Company S.r.l."}""", 0);

        if (path == "company" && method == HttpMethod.Put)
            return ("""{"id":1,"created":"2025-01-15T10:00:00Z","version":2,"user_id":1,"vat":"IT01234567890","fiscal_code":"01234567890","name":"Acme Updated S.r.l."}""", 0);

        // DELETE /company/{id}
        if (Regex.IsMatch(path, @"^company/\d+$") && method == HttpMethod.Delete)
            return ("""{"id":1}""", 0);

        // GET /company
        if (path == "company")
            return (LoadFixture("companies.json"), 2);

        // POST /send/file (upload)
        if (path == "send/file" && method == HttpMethod.Post)
            return ("""{"id":10,"companyId":1,"identifier":"IT01234567890_00099","fileName":"IT01234567890_00099.xml"}""", 0);

        // GET /send/{id}
        if (Regex.IsMatch(path, @"^send/\d+$"))
        {
            var id = path.Split('/')[1];
            var name = $"send-{id}.json";
            var json = File.Exists(Path.Combine(FixturesDir, name))
                ? LoadFixture(name)
                : LoadFixture("send-1.json");
            return (json, 0);
        }

        // GET /send (list)
        if (path == "send")
            return (LoadFixture("sent-invoices.json"), 3);

        // GET /receive/{id}
        if (Regex.IsMatch(path, @"^receive/\d+$"))
        {
            var id = path.Split('/')[1];
            var name = $"receive-{id}.json";
            var json = File.Exists(Path.Combine(FixturesDir, name))
                ? LoadFixture(name)
                : LoadFixture("receive-1.json");
            return (json, 0);
        }

        // GET /receive (list) — handle unread filter for dashboard
        if (path == "receive")
        {
            if (query.Contains("unread=true"))
            {
                var allJson = LoadFixture("received-invoices.json");
                var items = JsonSerializer.Deserialize<JsonElement[]>(allJson)!;
                var unread = items
                    .Where(i => !i.GetProperty("is_read").GetBoolean())
                    .ToArray();
                return (JsonSerializer.Serialize(unread), unread.Length);
            }

            return (LoadFixture("received-invoices.json"), 3);
        }

        // GET /update — handle send_id filter for invoice detail
        if (path == "update")
        {
            var sendIdMatch = Regex.Match(query, @"send_id=(\d+)");
            if (sendIdMatch.Success)
            {
                var sendId = int.Parse(sendIdMatch.Groups[1].Value);
                var allJson = LoadFixture("updates.json");
                var items = JsonSerializer.Deserialize<JsonElement[]>(allJson)!;
                var filtered = items
                    .Where(u => u.GetProperty("send_id").GetInt32() == sendId)
                    .ToArray();
                return (JsonSerializer.Serialize(filtered), filtered.Length);
            }

            return (LoadFixture("updates.json"), 4);
        }

        // Fallback: empty array
        return ("[]", 0);
    }
}
