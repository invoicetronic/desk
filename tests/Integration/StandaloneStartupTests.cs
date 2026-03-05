using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Desk.Tests.Integration;

public class StandaloneStartupTests : IClassFixture<StandaloneStartupTests.StandaloneFactory>
{
    private readonly HttpClient _client;

    public StandaloneStartupTests(StandaloneFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Fact]
    public async Task App_StartsWithoutDb_InStandaloneMode()
    {
        var response = await _client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task App_ServesStaticFiles_InStandaloneMode()
    {
        var response = await _client.GetAsync("/css/layout.css");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task OperativePages_AccessibleWithoutAuth_InStandaloneMode()
    {
        var response = await _client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task IdentityPages_RedirectToHome_InStandaloneMode()
    {
        var response = await _client.GetAsync("/Identity/Account/Login");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task StripeWebhook_Returns404_InStandaloneMode()
    {
        var response = await _client.PostAsync("/api/stripe/webhook",
            new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    public class StandaloneFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureLogging(l => l.SetMinimumLevel(LogLevel.Warning));
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton(new DeskConfig
                {
                    ApiKey = "itk_test_standalone",
                    ApiUrl = "https://api.invoicetronic.com"
                });
            });
        }
    }
}
