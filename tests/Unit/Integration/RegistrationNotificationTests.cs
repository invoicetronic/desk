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

public class RegistrationNotificationTests : IClassFixture<RegistrationNotificationTests.NotifyFactory>, IDisposable
{
    private readonly NotifyFactory _factory;
    private readonly HttpClient _client;

    public RegistrationNotificationTests(NotifyFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Fact]
    public async Task Register_SendsAdminNotification_WhenNotifyEmailConfigured()
    {
        var getResponse = await _client.GetAsync("/Identity/Account/Register");
        var html = await getResponse.Content.ReadAsStringAsync();

        const string marker = "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"";
        var start = html.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
        var end = html.IndexOf('"', start);
        var token = html[start..end];

        var email = $"notify-test-{Guid.NewGuid():N}@example.com";
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Email"] = email,
            ["Input.DisplayName"] = "Notify Test",
            ["Input.Password"] = "Test123!",
            ["Input.ConfirmPassword"] = "Test123!",
            ["__RequestVerificationToken"] = token
        });

        var cookies = getResponse.Headers.GetValues("Set-Cookie");
        var request = new HttpRequestMessage(HttpMethod.Post, "/Identity/Account/Register") { Content = form };
        foreach (var cookie in cookies)
            request.Headers.Add("Cookie", cookie.Split(';')[0]);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Identity/Account/Manage", response.Headers.Location?.OriginalString ?? "");

        var spy = _factory.EmailSpy;
        Assert.True(spy.RegistrationNotifyCalled, "SendRegistrationAdminNotifyAsync should have been called");
        Assert.Equal(email, spy.LastRegistrationEmail);
        Assert.Equal("Notify Test", spy.LastRegistrationDisplayName);
    }

    public void Dispose()
    {
        _client.Dispose();
        GC.SuppressFinalize(this);
    }

    public class SpyEmailService(DeskConfig config, ILogger<EmailService> logger) : EmailService(config, logger)
    {
        public bool RegistrationNotifyCalled { get; private set; }
        public string? LastRegistrationEmail { get; private set; }
        public string? LastRegistrationDisplayName { get; private set; }

        public override Task SendRegistrationAdminNotifyAsync(string userEmail, string displayName)
        {
            RegistrationNotifyCalled = true;
            LastRegistrationEmail = userEmail;
            LastRegistrationDisplayName = displayName;
            return Task.CompletedTask;
        }
    }

    public class NotifyFactory : WebApplicationFactory<Program>
    {
        public SpyEmailService EmailSpy { get; private set; } = null!;

        public string DbPath { get; } = Path.Combine(Path.GetTempPath(), $"desk_notify_test_{Guid.NewGuid()}.db");

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
                    Smtp = new SmtpConfig
                    {
                        Host = "smtp.example.com",
                        SenderEmail = "noreply@example.com",
                        NotifyEmail = "admin@example.com"
                    }
                });

                services.AddDbContext<DeskDbContext>(o => o.UseSqlite($"Data Source={DbPath}"));

                services.AddHttpClient<ApiClient>()
                    .ConfigurePrimaryHttpMessageHandler(() => new IntegrationApiHandler());

                services.AddScoped<EmailService>(sp =>
                {
                    var cfg = sp.GetRequiredService<DeskConfig>();
                    var log = sp.GetRequiredService<ILogger<EmailService>>();
                    var spy = new SpyEmailService(cfg, log);
                    EmailSpy = spy;
                    return spy;
                });
            });
        }
    }
}
