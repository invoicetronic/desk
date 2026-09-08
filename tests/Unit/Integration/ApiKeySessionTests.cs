using System.Net;
using Desk.Data;
using Desk.Tests.Helpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Desk.Tests.Integration;

/// <summary>
/// The API key kept in session must never be an empty string: an empty key looks
/// like "a key is set" to the seat guard, which then calls the API without
/// credentials, gets a 401 and strands the user on /NoSeat.
/// </summary>
public class ApiKeySessionTests
{
    [Fact]
    public async Task RejectedSave_WithNoPreviousKey_DoesNotStrandUserOnNoSeat()
    {
        var handler = new RecordingApiHandler(hasActiveSeat: false);
        await using var factory = new ApiKeySessionFactory(handler);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var cookies = await RegisterAndGetCookies(client);

        // The user has no key stored yet and saves a live key without a seat: rejected.
        var saveResponse = await SaveApiKey(client, cookies, "ik_live_keywithoutseat00000000000");
        Assert.Equal(HttpStatusCode.OK, saveResponse.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DeskDbContext>();
            Assert.True(string.IsNullOrEmpty(db.Users.Single().ApiKey), "the key must not be persisted");
        }

        handler.ClearCalls();

        // Browsing any app page must send the user back to the profile, not to /NoSeat,
        // and must not produce an unauthenticated API call.
        var home = await SendWithCookies(client, HttpMethod.Get, "/", cookies);

        Assert.DoesNotContain("/NoSeat", home.Headers.Location?.OriginalString ?? "");
        Assert.Contains("/Identity/Account/Manage", home.Headers.Location?.OriginalString ?? "");
        Assert.DoesNotContain(handler.Calls, c => string.IsNullOrEmpty(c.User));
    }

    [Fact]
    public async Task ExpiredSession_WithStoredKey_StillSendsAuthenticatedRequests()
    {
        var handler = new RecordingApiHandler(hasActiveSeat: true);
        await using var factory = new ApiKeySessionFactory(handler);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var cookies = await RegisterAndGetCookies(client);

        const string apiKey = "ik_live_storedkey000000000000000";
        var saveResponse = await SaveApiKey(client, cookies, apiKey);
        Assert.Equal(HttpStatusCode.OK, saveResponse.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DeskDbContext>();
            Assert.False(string.IsNullOrEmpty(db.Users.Single().ApiKey), "the key must be persisted");
        }

        // Drop the session cookie, keep the authentication cookie: the key is reloaded
        // from the database and must reach the API.
        var authOnly = cookies.Where(c => c.StartsWith(".AspNetCore.Identity.Application")).ToList();
        Assert.NotEmpty(authOnly);

        handler.ClearCalls();

        var home = await SendWithCookies(client, HttpMethod.Get, "/", authOnly);

        Assert.Equal(HttpStatusCode.OK, home.StatusCode);
        Assert.Contains(handler.Calls, c => c.Path == "status" && c.User == apiKey);
        Assert.DoesNotContain(handler.Calls, c => string.IsNullOrEmpty(c.User));
    }

    private static async Task<HttpResponseMessage> SaveApiKey(HttpClient client, List<string> cookies, string apiKey)
    {
        var getResponse = await SendWithCookies(client, HttpMethod.Get, "/Identity/Account/Manage", cookies);
        Collect(cookies, getResponse);

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["ApiKeyInput"] = apiKey,
            ["__RequestVerificationToken"] = ExtractAntiforgeryToken(await getResponse.Content.ReadAsStringAsync())
        });

        var request = new HttpRequestMessage(HttpMethod.Post, "/Identity/Account/Manage?handler=SaveApiKey")
        {
            Content = form
        };
        foreach (var cookie in cookies)
            request.Headers.Add("Cookie", cookie);

        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> SendWithCookies(
        HttpClient client, HttpMethod method, string url, List<string> cookies)
    {
        var request = new HttpRequestMessage(method, url);
        foreach (var cookie in cookies)
            request.Headers.Add("Cookie", cookie);
        return await client.SendAsync(request);
    }

    private static void Collect(List<string> cookies, HttpResponseMessage response)
    {
        if (response.Headers.TryGetValues("Set-Cookie", out var set))
            cookies.AddRange(set.Select(c => c.Split(';')[0]));
    }

    private static string ExtractAntiforgeryToken(string html)
    {
        const string marker = "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"";
        var start = html.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
        var end = html.IndexOf('"', start);
        return html[start..end];
    }

    private static async Task<List<string>> RegisterAndGetCookies(HttpClient client)
    {
        var getResponse = await client.GetAsync("/Identity/Account/Register");
        var token = ExtractAntiforgeryToken(await getResponse.Content.ReadAsStringAsync());

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Email"] = $"test-{Guid.NewGuid():N}@example.com",
            ["Input.DisplayName"] = "Test User",
            ["Input.Password"] = "Test123!",
            ["Input.ConfirmPassword"] = "Test123!",
            ["__RequestVerificationToken"] = token
        });

        var request = new HttpRequestMessage(HttpMethod.Post, "/Identity/Account/Register") { Content = form };
        var cookies = getResponse.Headers.GetValues("Set-Cookie").Select(c => c.Split(';')[0]).ToList();
        foreach (var cookie in cookies)
            request.Headers.Add("Cookie", cookie);

        var response = await client.SendAsync(request);
        Collect(cookies, response);

        return cookies;
    }

    private class ApiKeySessionFactory(HttpMessageHandler handler) : WebApplicationFactory<Program>
    {
        private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"desk_apikey_session_{Guid.NewGuid()}.db");

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureLogging(l => l.SetMinimumLevel(LogLevel.Warning));
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton(new DeskConfig
                {
                    ApiUrl = "https://api.invoicetronic.com",
                    IsHosted = true,
                    Database = new DatabaseConfig
                    {
                        Provider = "sqlite",
                        ConnectionString = $"Data Source={_dbPath}"
                    }
                });

                services.AddDbContext<DeskDbContext>(o => o.UseSqlite($"Data Source={_dbPath}"));

                services.AddHttpClient<ApiClient>()
                    .ConfigurePrimaryHttpMessageHandler(() => handler);
            });
        }
    }
}
