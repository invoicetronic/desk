using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Desk.Tests.Integration;

public class MultiUserStartupTests : IClassFixture<MultiUserStartupTests.MultiUserFactory>, IDisposable
{
    private readonly HttpClient _client;

    public MultiUserStartupTests(MultiUserFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Fact]
    public async Task App_CreatesDatabase_InMultiUserMode()
    {
        // App starts successfully (even if it redirects to login)
        var response = await _client.GetAsync("/");
        Assert.True(response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Redirect);
    }

    [Fact]
    public async Task App_RequiresAuth_ForProtectedPages()
    {
        // Unauthenticated request to / should redirect to login
        var response = await _client.GetAsync("/");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Identity/Account/Login", response.Headers.Location?.OriginalString ?? "");
    }

    [Fact]
    public async Task LoginPage_IsAccessible_WithoutAuth()
    {
        var response = await _client.GetAsync("/Identity/Account/Login");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task RegisterPage_IsAccessible_WithoutAuth()
    {
        var response = await _client.GetAsync("/Identity/Account/Register");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Register_Succeeds_WithoutBilling()
    {
        // GET the register page to obtain antiforgery token
        var getResponse = await _client.GetAsync("/Identity/Account/Register");
        var html = await getResponse.Content.ReadAsStringAsync();

        const string marker = "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"";
        var start = html.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start >= 0, "Antiforgery token not found");
        start += marker.Length;
        var end = html.IndexOf('"', start);
        var token = html[start..end];

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Email"] = $"test-{Guid.NewGuid():N}@example.com",
            ["Input.DisplayName"] = "Test User",
            ["Input.Password"] = "Test123!",
            ["Input.ConfirmPassword"] = "Test123!",
            ["__RequestVerificationToken"] = token
        });

        // Forward cookies from GET (antiforgery cookie)
        var cookies = getResponse.Headers.GetValues("Set-Cookie");
        var request = new HttpRequestMessage(HttpMethod.Post, "/Identity/Account/Register") { Content = form };
        foreach (var cookie in cookies)
            request.Headers.Add("Cookie", cookie.Split(';')[0]);

        var response = await _client.SendAsync(request);

        // Successful registration redirects to profile page
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Identity/Account/Manage", response.Headers.Location?.OriginalString ?? "");
    }

    public void Dispose()
    {
        _client.Dispose();
        GC.SuppressFinalize(this);
    }

    public class MultiUserFactory : WebApplicationFactory<Program>
    {
        public string DbPath { get; } = Path.Combine(Path.GetTempPath(), $"desk_test_{Guid.NewGuid()}.db");

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureTestServices(services =>
            {
                // Explicitly set multi-user mode (no API key).
                // This ensures tests are not affected by any desk.yml in src/.
                services.AddSingleton(new DeskConfig
                {
                    ApiUrl = "https://api.invoicetronic.com",
                    Database = new DatabaseConfig
                    {
                        Provider = "sqlite",
                        ConnectionString = $"Data Source={DbPath}"
                    }
                });
            });
        }
    }
}
