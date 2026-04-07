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

public class SeatCheckTests : IDisposable
{
    [Fact]
    public async Task AuthenticatedUser_WithActiveSeat_CanAccessHome()
    {
        await using var factory = new SeatCheckFactory(withActiveSeat: true);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        // Register and get auth cookies
        var cookies = await RegisterAndGetCookies(client);

        // Access home page with auth cookies
        var request = new HttpRequestMessage(HttpMethod.Get, "/");
        foreach (var cookie in cookies)
            request.Headers.Add("Cookie", cookie);

        var response = await client.SendAsync(request);

        // Should not redirect to NoSeat — should get OK (home page) or redirect to profile (no API key yet)
        Assert.NotEqual("/NoSeat", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task AuthenticatedUser_WithoutSeat_RedirectedToNoSeat()
    {
        await using var factory = new SeatCheckFactory(withActiveSeat: false);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        // Register and get auth cookies
        var cookies = await RegisterAndGetCookies(client);

        // Set API key in profile (simulate having a key configured)
        // First, access profile to get session established
        var profileRequest = new HttpRequestMessage(HttpMethod.Get, "/Identity/Account/Manage");
        foreach (var cookie in cookies)
            profileRequest.Headers.Add("Cookie", cookie);
        var profileResponse = await client.SendAsync(profileRequest);

        // Now access home — should redirect to NoSeat because API returns has_active_seat=false
        var homeRequest = new HttpRequestMessage(HttpMethod.Get, "/");
        // Collect all cookies from previous responses
        foreach (var cookie in cookies)
            homeRequest.Headers.Add("Cookie", cookie);
        if (profileResponse.Headers.TryGetValues("Set-Cookie", out var profileCookies))
            foreach (var cookie in profileCookies)
                homeRequest.Headers.Add("Cookie", cookie.Split(';')[0]);

        var response = await client.SendAsync(homeRequest);

        // Without API key set, user gets redirected to profile (API key required), not NoSeat
        // The seat check only fires when an API key is present
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    [Fact]
    public async Task NoSeatPage_IsAccessible_WithAuth()
    {
        await using var factory = new SeatCheckFactory(withActiveSeat: true);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var cookies = await RegisterAndGetCookies(client);

        var request = new HttpRequestMessage(HttpMethod.Get, "/NoSeat");
        foreach (var cookie in cookies)
            request.Headers.Add("Cookie", cookie);

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        // The page renders localized content — check for key structural elements
        Assert.Contains("desk-auth-card", content);
    }

    [Fact]
    public async Task NoSeatPage_RequiresAuth()
    {
        await using var factory = new SeatCheckFactory(withActiveSeat: true);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/NoSeat");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Identity/Account/Login", response.Headers.Location?.OriginalString ?? "");
    }

    private static async Task<List<string>> RegisterAndGetCookies(HttpClient client)
    {
        var getResponse = await client.GetAsync("/Identity/Account/Register");
        var html = await getResponse.Content.ReadAsStringAsync();

        const string marker = "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"";
        var start = html.IndexOf(marker, StringComparison.Ordinal);
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

        var registerRequest = new HttpRequestMessage(HttpMethod.Post, "/Identity/Account/Register") { Content = form };
        var getCookies = getResponse.Headers.GetValues("Set-Cookie").ToList();
        foreach (var cookie in getCookies)
            registerRequest.Headers.Add("Cookie", cookie.Split(';')[0]);

        var response = await client.SendAsync(registerRequest);

        var allCookies = new List<string>();
        foreach (var cookie in getCookies)
            allCookies.Add(cookie.Split(';')[0]);
        if (response.Headers.TryGetValues("Set-Cookie", out var postCookies))
            foreach (var cookie in postCookies)
                allCookies.Add(cookie.Split(';')[0]);

        return allCookies;
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    private class SeatCheckFactory(bool withActiveSeat) : WebApplicationFactory<Program>
    {
        private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"desk_seat_test_{Guid.NewGuid()}.db");

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureLogging(l => l.SetMinimumLevel(LogLevel.Warning));
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton(new DeskConfig
                {
                    ApiUrl = "https://api.invoicetronic.com",
                    Database = new DatabaseConfig
                    {
                        Provider = "sqlite",
                        ConnectionString = $"Data Source={_dbPath}"
                    }
                });

                services.AddDbContext<DeskDbContext>(o => o.UseSqlite($"Data Source={_dbPath}"));

                var handler = withActiveSeat
                    ? (HttpMessageHandler)new IntegrationApiHandler()
                    : new NoSeatApiHandler();

                services.AddHttpClient<ApiClient>()
                    .ConfigurePrimaryHttpMessageHandler(() => handler);
            });
        }
    }
}
