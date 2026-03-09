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

public class PasswordResetTests : IClassFixture<PasswordResetTests.SmtpFactory>, IDisposable
{
    private readonly HttpClient _client;

    public PasswordResetTests(SmtpFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Fact]
    public async Task ForgotPassword_IsAccessible_WhenSmtpConfigured()
    {
        var response = await _client.GetAsync("/Identity/Account/ForgotPassword");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_ReturnsBadRequest_WithoutToken()
    {
        var response = await _client.GetAsync("/Identity/Account/ResetPassword");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_IsAccessible_WithToken()
    {
        var response = await _client.GetAsync("/Identity/Account/ResetPassword?token=fake&email=test@example.com");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ForgotPasswordConfirmation_IsAccessible()
    {
        var response = await _client.GetAsync("/Identity/Account/ForgotPasswordConfirmation");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ResetPasswordConfirmation_IsAccessible()
    {
        var response = await _client.GetAsync("/Identity/Account/ResetPasswordConfirmation");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    public void Dispose()
    {
        _client.Dispose();
        GC.SuppressFinalize(this);
    }

    public class SmtpFactory : WebApplicationFactory<Program>
    {
        public string DbPath { get; } = Path.Combine(Path.GetTempPath(), $"desk_smtp_test_{Guid.NewGuid()}.db");

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
                        SenderEmail = "noreply@example.com"
                    }
                });

                services.AddDbContext<DeskDbContext>(o => o.UseSqlite($"Data Source={DbPath}"));

                services.AddHttpClient<ApiClient>()
                    .ConfigurePrimaryHttpMessageHandler(() => new IntegrationApiHandler());
            });
        }
    }
}

public class PasswordResetNoSmtpTests : IClassFixture<PasswordResetNoSmtpTests.NoSmtpFactory>, IDisposable
{
    private readonly HttpClient _client;

    public PasswordResetNoSmtpTests(NoSmtpFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Fact]
    public async Task ForgotPassword_RedirectsToLogin_WhenSmtpNotConfigured()
    {
        var response = await _client.GetAsync("/Identity/Account/ForgotPassword");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location?.OriginalString ?? "");
    }

    [Fact]
    public async Task LoginPage_DoesNotShowForgotLink_WhenSmtpNotConfigured()
    {
        var response = await _client.GetAsync("/Identity/Account/Login");
        var html = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("ForgotPassword", html);
    }

    public void Dispose()
    {
        _client.Dispose();
        GC.SuppressFinalize(this);
    }

    public class NoSmtpFactory : WebApplicationFactory<Program>
    {
        public string DbPath { get; } = Path.Combine(Path.GetTempPath(), $"desk_nosmtp_test_{Guid.NewGuid()}.db");

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
                    }
                });

                services.AddDbContext<DeskDbContext>(o => o.UseSqlite($"Data Source={DbPath}"));

                services.AddHttpClient<ApiClient>()
                    .ConfigurePrimaryHttpMessageHandler(() => new IntegrationApiHandler());
            });
        }
    }
}

public class PasswordResetStandaloneTests : IClassFixture<PasswordResetStandaloneTests.StandaloneSmtpFactory>
{
    private readonly HttpClient _client;

    public PasswordResetStandaloneTests(StandaloneSmtpFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Fact]
    public async Task ForgotPassword_RedirectsToHome_InStandaloneMode()
    {
        var response = await _client.GetAsync("/Identity/Account/ForgotPassword");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task ResetPassword_RedirectsToHome_InStandaloneMode()
    {
        var response = await _client.GetAsync("/Identity/Account/ResetPassword?token=fake");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/", response.Headers.Location?.OriginalString);
    }

    public class StandaloneSmtpFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureLogging(l => l.SetMinimumLevel(LogLevel.Warning));
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton(new DeskConfig
                {
                    ApiKey = "itk_test_standalone",
                    ApiUrl = "https://api.invoicetronic.com",
                    Smtp = new SmtpConfig
                    {
                        Host = "smtp.example.com",
                        SenderEmail = "noreply@example.com"
                    }
                });

                services.AddHttpClient<ApiClient>()
                    .ConfigurePrimaryHttpMessageHandler(() => new IntegrationApiHandler());
            });
        }
    }
}
