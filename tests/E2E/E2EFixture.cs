using System.Net.Http.Headers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace Desk.E2E;

[CollectionDefinition("E2E")]
public class E2ECollection : ICollectionFixture<E2EFixture>;

/// <summary>
/// Shared fixture for all E2E tests.
/// Starts the ASP.NET app via WebApplicationFactory (TestServer) and
/// a lightweight Kestrel reverse proxy that Playwright connects to.
/// </summary>
public class E2EFixture : IAsyncLifetime
{
    private DeskTestFactory? _factory;
    private WebApplication? _proxy;
    private IPlaywright? _playwright;

    public string ServerAddress { get; private set; } = "";
    public IBrowser Browser { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        _factory = new DeskTestFactory();

        // WAF client that does NOT handle cookies or redirects — let Playwright manage those
        var testClient = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false
        });

        // Start a Kestrel reverse proxy that forwards to the TestServer
        var proxyBuilder = WebApplication.CreateSlimBuilder();
        proxyBuilder.WebHost.UseUrls("http://127.0.0.1:0");
        proxyBuilder.Logging.SetMinimumLevel(LogLevel.Warning);
        _proxy = proxyBuilder.Build();

        _proxy.Run(async context =>
        {
            var targetUri = $"{context.Request.Path}{context.Request.QueryString}";
            var forward = new HttpRequestMessage(
                new HttpMethod(context.Request.Method), targetUri);

            // Copy request headers (skip Host — testClient sets its own)
            foreach (var header in context.Request.Headers)
            {
                if (header.Key.Equals("Host", StringComparison.OrdinalIgnoreCase))
                    continue;
                forward.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
            }

            // Copy request body
            if (context.Request.ContentLength > 0 || context.Request.ContentType is not null)
            {
                forward.Content = new StreamContent(context.Request.Body);
                if (context.Request.ContentType is not null)
                    forward.Content.Headers.ContentType =
                        MediaTypeHeaderValue.Parse(context.Request.ContentType);
            }

            using var response = await testClient.SendAsync(forward);

            context.Response.StatusCode = (int)response.StatusCode;

            // Copy response headers
            foreach (var header in response.Headers)
                context.Response.Headers[header.Key] = header.Value.ToArray();
            foreach (var header in response.Content.Headers)
                context.Response.Headers[header.Key] = header.Value.ToArray();
            context.Response.Headers.Remove("Transfer-Encoding");

            await response.Content.CopyToAsync(context.Response.Body);
        });

        await _proxy.StartAsync();
        ServerAddress = _proxy.Urls.First();

        _playwright = await Playwright.CreateAsync();
        Browser = await _playwright.Chromium.LaunchAsync(new() { Headless = true });
    }

    public async Task DisposeAsync()
    {
        if (Browser is not null)
            await Browser.CloseAsync();

        _playwright?.Dispose();

        if (_proxy is not null)
            await _proxy.StopAsync();

        _factory?.Dispose();
    }

    public async Task<IPage> CreatePageAsync()
    {
        var context = await Browser.NewContextAsync();
        return await context.NewPageAsync();
    }
}

/// <summary>
/// Standard WebApplicationFactory with mock API handler.
/// Uses TestServer (no Kestrel) — accessed by the reverse proxy.
/// </summary>
public class DeskTestFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton(new DeskConfig
            {
                ApiKey = "itk_test_e2e",
                ApiUrl = "https://mock.api.test"
            });

            services.AddHttpClient<ApiClient>()
                .ConfigurePrimaryHttpMessageHandler(() => new FixtureApiHandler())
                .SetHandlerLifetime(Timeout.InfiniteTimeSpan);
        });

        builder.ConfigureLogging(l => l.SetMinimumLevel(LogLevel.Warning));
    }
}
