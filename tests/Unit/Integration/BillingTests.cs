using System.Net;
using Desk.Data;
using Desk.Tests.Helpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Desk.Tests.Integration;

public class BillingTests : IClassFixture<BillingTests.BillingFactory>, IDisposable
{
    private readonly BillingFactory _factory;
    private readonly HttpClient _client;

    public BillingTests(BillingFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Fact]
    public async Task ProtectedPage_RedirectsToLogin_WhenNotAuthenticated()
    {
        var response = await _client.GetAsync("/");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Identity/Account/Login", response.Headers.Location?.OriginalString ?? "");
    }

    [Fact]
    public async Task StripeWebhook_Returns400_ForInvalidSignature()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/stripe/webhook")
        {
            Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
        };
        request.Headers.Add("Stripe-Signature", "invalid_signature");

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task StripeWebhook_IsAccessible_WhenBillingEnabled()
    {
        // Without Stripe-Signature header, should still return 400 (not 404)
        var response = await _client.PostAsync("/api/stripe/webhook",
            new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SubscribePage_IsAccessible_WhenAuthenticated()
    {
        var client = await CreateAuthenticatedClientAsync();
        var response = await client.GetAsync("/Billing/Subscribe");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedPage_Accessible_DuringPreviewPeriod()
    {
        var client = await CreateAuthenticatedClientAsync();
        var response = await client.GetAsync("/");
        // New user is in preview period, should not redirect to Subscribe
        var location = response.Headers.Location?.OriginalString ?? "";
        Assert.DoesNotContain("/Billing/Subscribe", location);
    }

    [Fact]
    public async Task ProtectedPage_RedirectsToSubscribe_WhenPreviewExpired()
    {
        var client = await CreateAuthenticatedClientAsync(createdAt: DateTime.UtcNow.AddDays(-20));
        var response = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Billing/Subscribe", response.Headers.Location?.OriginalString ?? "");
    }

    [Fact]
    public async Task ProtectedPage_Accessible_WhenSubscriptionActive()
    {
        var client = await CreateAuthenticatedClientAsync(subscriptionStatus: "active", createdAt: DateTime.UtcNow.AddDays(-30));
        var response = await client.GetAsync("/");
        // Should not redirect to Subscribe (may redirect to profile for API key, that's fine)
        var location = response.Headers.Location?.OriginalString ?? "";
        Assert.DoesNotContain("/Billing/Subscribe", location);
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync(string? subscriptionStatus = null, DateTime? createdAt = null)
    {
        var email = $"test-{Guid.NewGuid():N}@example.com";

        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<DeskUser>>();
        var user = new DeskUser
        {
            UserName = email,
            Email = email,
            DisplayName = "Test",
            ApiKey = "itk_test_billing_key",
            SubscriptionStatus = subscriptionStatus,
            CreatedAt = createdAt ?? DateTime.UtcNow
        };
        await userManager.CreateAsync(user, "Test123!");

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        // Login via form post
        var loginPage = await client.GetAsync("/Identity/Account/Login");
        var loginContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Email"] = email,
            ["Input.Password"] = "Test123!",
            ["Input.RememberMe"] = "false"
        });

        // Get antiforgery token from login page
        var loginHtml = await loginPage.Content.ReadAsStringAsync();
        var token = ExtractAntiForgeryToken(loginHtml);
        if (token is not null)
        {
            loginContent = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Input.Email"] = email,
                ["Input.Password"] = "Test123!",
                ["Input.RememberMe"] = "false",
                ["__RequestVerificationToken"] = token
            });
        }

        var loginResponse = await client.PostAsync("/Identity/Account/Login", loginContent);

        // Follow the redirect to capture cookies
        if (loginResponse.StatusCode == HttpStatusCode.Redirect)
        {
            var redirectUrl = loginResponse.Headers.Location?.OriginalString ?? "/";
            await client.GetAsync(redirectUrl);
        }

        return client;
    }

    private static string? ExtractAntiForgeryToken(string html)
    {
        const string marker = "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"";
        var start = html.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0) return null;
        start += marker.Length;
        var end = html.IndexOf('"', start);
        return end > start ? html[start..end] : null;
    }

    public void Dispose()
    {
        _client.Dispose();
        GC.SuppressFinalize(this);
    }

    public class BillingFactory : WebApplicationFactory<Program>
    {
        public string DbPath { get; } = Path.Combine(Path.GetTempPath(), $"desk_billing_test_{Guid.NewGuid()}.db");

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
                        ConnectionString = $"Data Source={DbPath}"
                    },
                    Stripe = new StripeConfig
                    {
                        SecretKey = "sk_test_fake_key_for_testing",
                        PublishableKey = "pk_test_fake_key_for_testing",
                        WebhookSecret = "whsec_fake_secret_for_testing",
                        PriceIdIt = "price_fake_it_for_testing",
                        PriceIdForeign = "price_fake_foreign_for_testing"
                    }
                });

                // Override DbContext to use unique test DB (Program.cs registers with default path)
                services.AddDbContext<DeskDbContext>(o => o.UseSqlite($"Data Source={DbPath}"));

                services.AddHttpClient<ApiClient>()
                    .ConfigurePrimaryHttpMessageHandler(() => new IntegrationApiHandler());
            });
        }
    }
}
