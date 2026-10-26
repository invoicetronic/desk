using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
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
/// End-to-end flow of two-factor authentication: activation with an authenticator app,
/// sign-in with a code or a recovery code, deactivation.
/// </summary>
public partial class TwoFactorTests
{
    private const string Password = "Test123!";

    [Fact]
    public async Task Enable_WithValidCode_TurnsOnTwoFactorAndShowsRecoveryCodes()
    {
        await using var factory = new TwoFactorFactory();
        var browser = new Browser(factory);
        await browser.Register();

        var (key, codes) = await EnableTwoFactor(browser);

        Assert.False(string.IsNullOrEmpty(key));
        Assert.Equal(10, codes.Count);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DeskDbContext>();
        Assert.True(db.Users.Single().TwoFactorEnabled);
    }

    [Fact]
    public async Task Enable_WithWrongCode_LeavesTwoFactorOff()
    {
        await using var factory = new TwoFactorFactory();
        var browser = new Browser(factory);
        await browser.Register();

        var page = await browser.Get("/Identity/Account/Manage/EnableAuthenticator");
        var response = await browser.Post("/Identity/Account/Manage/EnableAuthenticator", page,
            new() { ["Input.Code"] = "000000" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DeskDbContext>();
        Assert.False(db.Users.Single().TwoFactorEnabled);
    }

    [Fact]
    public async Task Login_WithTwoFactor_AsksForCodeAndSignsInOnlyWithValidOne()
    {
        await using var factory = new TwoFactorFactory();
        var setup = new Browser(factory);
        var email = await setup.Register();
        var (key, _) = await EnableTwoFactor(setup);

        var browser = new Browser(factory);
        var login = await browser.Login(email);
        Assert.Contains("/Identity/Account/LoginWith2fa", login.Headers.Location?.OriginalString);

        // The password alone does not sign in
        var home = await browser.Get("/");
        Assert.Contains("/Identity/Account/Login", home.Headers.Location?.OriginalString);

        var page = await browser.Get(login.Headers.Location!.OriginalString);
        var wrong = await browser.Post(login.Headers.Location!.OriginalString, page,
            new() { ["Input.TwoFactorCode"] = "000000" });
        Assert.Equal(HttpStatusCode.OK, wrong.StatusCode);

        var right = await browser.Post(login.Headers.Location!.OriginalString, page,
            new() { ["Input.TwoFactorCode"] = Totp(key) });
        Assert.Equal(HttpStatusCode.Redirect, right.StatusCode);

        var profile = await browser.Get("/Identity/Account/Manage");
        Assert.Equal(HttpStatusCode.OK, profile.StatusCode);
    }

    [Fact]
    public async Task LoginWith2fa_WithoutPasswordStep_RedirectsToLogin()
    {
        await using var factory = new TwoFactorFactory();
        var browser = new Browser(factory);

        var response = await browser.Get("/Identity/Account/LoginWith2fa");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Identity/Account/Login", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task RecoveryCode_SignsInOnlyOnce()
    {
        await using var factory = new TwoFactorFactory();
        var setup = new Browser(factory);
        var email = await setup.Register();
        var (_, codes) = await EnableTwoFactor(setup);

        Assert.Equal(HttpStatusCode.Redirect, (await RecoveryLogin(factory, email, codes[0])).StatusCode);
        // Already used: rejected, the page is shown again with the error
        Assert.Equal(HttpStatusCode.OK, (await RecoveryLogin(factory, email, codes[0])).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await RecoveryLogin(factory, email, codes[1])).StatusCode);
    }

    [Fact]
    public async Task Disable_TurnsOffTwoFactorAndReplacesTheKey()
    {
        await using var factory = new TwoFactorFactory();
        var browser = new Browser(factory);
        var email = await browser.Register();
        var (key, _) = await EnableTwoFactor(browser);

        var page = await browser.Get("/Identity/Account/Manage/TwoFactor");
        var disable = await browser.Post("/Identity/Account/Manage/TwoFactor?handler=Disable", page, []);
        Assert.Equal(HttpStatusCode.Redirect, disable.StatusCode);

        // The session survives the security stamp change
        Assert.Equal(HttpStatusCode.OK, (await browser.Get("/Identity/Account/Manage")).StatusCode);

        // The password alone signs in again
        var other = new Browser(factory);
        var login = await other.Login(email);
        Assert.DoesNotContain("LoginWith2fa", login.Headers.Location?.OriginalString ?? "");

        // A new activation shows a different key
        var enable = await browser.Get("/Identity/Account/Manage/EnableAuthenticator");
        Assert.NotEqual(key, SharedKey(await enable.Content.ReadAsStringAsync()));
    }

    private static async Task<(string Key, List<string> Codes)> EnableTwoFactor(Browser browser)
    {
        var page = await browser.Get("/Identity/Account/Manage/EnableAuthenticator");
        var key = SharedKey(await page.Content.ReadAsStringAsync());

        var response = await browser.Post("/Identity/Account/Manage/EnableAuthenticator", page,
            new() { ["Input.Code"] = Totp(key) });
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var twoFactor = await browser.Get("/Identity/Account/Manage/TwoFactor");
        var html = await twoFactor.Content.ReadAsStringAsync();
        var block = html[html.IndexOf("id=\"recoveryCodes\"", StringComparison.Ordinal)..];
        block = block[..block.IndexOf("</div>", StringComparison.Ordinal)];
        var codes = SpanRegex().Matches(block).Select(m => m.Groups[1].Value).ToList();

        return (key, codes);
    }

    private static async Task<HttpResponseMessage> RecoveryLogin(TwoFactorFactory factory, string email, string code)
    {
        var browser = new Browser(factory);
        _ = await browser.Login(email);
        var page = await browser.Get("/Identity/Account/LoginWithRecoveryCode");
        return await browser.Post("/Identity/Account/LoginWithRecoveryCode", page,
            new() { ["Input.RecoveryCode"] = code });
    }

    private static string SharedKey(string html) =>
        SharedKeyRegex().Match(html).Groups[1].Value.Replace(" ", "").ToUpperInvariant();

    /// <summary>
    /// RFC 6238 code for the current time step, as an authenticator app would show it.
    /// </summary>
    private static string Totp(string base32Key)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var bits = string.Concat(base32Key.TrimEnd('=').Select(c => Convert.ToString(alphabet.IndexOf(c), 2).PadLeft(5, '0')));
        var key = Enumerable.Range(0, bits.Length / 8).Select(i => Convert.ToByte(bits.Substring(i * 8, 8), 2)).ToArray();

        var step = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30;
        var counter = BitConverter.GetBytes(step);
        if (BitConverter.IsLittleEndian) Array.Reverse(counter);

        var hash = HMACSHA1.HashData(key, counter);
        var offset = hash[^1] & 0x0F;
        var binary = ((hash[offset] & 0x7F) << 24) | (hash[offset + 1] << 16) | (hash[offset + 2] << 8) | hash[offset + 3];
        return (binary % 1_000_000).ToString("D6");
    }

    [GeneratedRegex("<code id=\"sharedKey\"[^>]*>([^<]+)</code>")]
    private static partial Regex SharedKeyRegex();

    [GeneratedRegex("<span>([^<]+)</span>")]
    private static partial Regex SpanRegex();

    /// <summary>
    /// A client with its own cookie jar: cookies set again by the server replace the old value.
    /// </summary>
    private class Browser(TwoFactorFactory factory)
    {
        private readonly HttpClient _client = factory.CreateClient(new() { AllowAutoRedirect = false });
        private readonly Dictionary<string, string> _cookies = [];

        public async Task<string> Register()
        {
            var email = $"test-{Guid.NewGuid():N}@example.com";
            var page = await Get("/Identity/Account/Register");
            _ = await Post("/Identity/Account/Register", page, new()
            {
                ["Input.Email"] = email,
                ["Input.DisplayName"] = "Test User",
                ["Input.Password"] = Password,
                ["Input.ConfirmPassword"] = Password
            });
            return email;
        }

        public async Task<HttpResponseMessage> Login(string email)
        {
            var page = await Get("/Identity/Account/Login");
            return await Post("/Identity/Account/Login", page, new()
            {
                ["Input.Email"] = email,
                ["Input.Password"] = Password
            });
        }

        public Task<HttpResponseMessage> Get(string url) => Send(new(HttpMethod.Get, url));

        public async Task<HttpResponseMessage> Post(string url, HttpResponseMessage page, Dictionary<string, string> fields)
        {
            fields["__RequestVerificationToken"] = AntiforgeryToken(await page.Content.ReadAsStringAsync());
            return await Send(new(HttpMethod.Post, url) { Content = new FormUrlEncodedContent(fields) });
        }

        private async Task<HttpResponseMessage> Send(HttpRequestMessage request)
        {
            if (_cookies.Count > 0)
                request.Headers.Add("Cookie", string.Join("; ", _cookies.Select(c => $"{c.Key}={c.Value}")));

            var response = await _client.SendAsync(request);
            if (response.Headers.TryGetValues("Set-Cookie", out var set))
            {
                foreach (var pair in set.Select(c => c.Split(';')[0].Split('=', 2)))
                {
                    if (string.IsNullOrEmpty(pair[1])) _ = _cookies.Remove(pair[0]);
                    else _cookies[pair[0]] = pair[1];
                }
            }

            return response;
        }

        private static string AntiforgeryToken(string html)
        {
            const string marker = "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"";
            var start = html.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
            return html[start..html.IndexOf('"', start)];
        }
    }

    private class TwoFactorFactory : WebApplicationFactory<Program>
    {
        private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"desk_two_factor_{Guid.NewGuid()}.db");

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

                services.AddHttpClient<ApiClient>()
                    .ConfigurePrimaryHttpMessageHandler(() => new RecordingApiHandler(hasActiveSeat: true));
            });
        }
    }
}
