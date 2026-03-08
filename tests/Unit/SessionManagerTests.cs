using Desk.Models;
using Desk.Tests.Helpers;

namespace Desk.Tests;

public class SessionManagerTests
{
    private static SessionManager CreateManager(DeskConfig? config = null)
    {
        config ??= new DeskConfig();
        var httpContext = new DefaultHttpContext { Session = new TestSession() };
        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        return new SessionManager(accessor, config);
    }

    [Fact]
    public void GetApiKey_ReturnsConfigKey_InStandaloneMode()
    {
        var config = new DeskConfig { ApiKey = "standalone-key" };
        var manager = CreateManager(config);

        Assert.Equal("standalone-key", manager.GetApiKey());
    }

    [Fact]
    public void GetApiKey_ReturnsSessionKey_InMultiUserMode()
    {
        var manager = CreateManager();
        manager.SetApiKey("session-key");

        Assert.Equal("session-key", manager.GetApiKey());
    }

    [Fact]
    public void GetApiKey_ReturnsNull_WhenNoKeySet()
    {
        var manager = CreateManager();

        Assert.Null(manager.GetApiKey());
    }

    [Fact]
    public void SetAndGetSelectedCompanyId_RoundTrips()
    {
        var manager = CreateManager();
        manager.SetSelectedCompanyId(42);

        Assert.Equal(42, manager.GetSelectedCompanyId());
    }

    [Fact]
    public void SetAndGetCompanies_RoundTrips()
    {
        var manager = CreateManager();
        List<Company> companies =
        [
            new() { Id = 1, Name = "Rossi Srl", Vat = "IT123", FiscalCode = "FC1" },
            new() { Id = 2, Name = "Bianchi Spa", Vat = "IT456", FiscalCode = "FC2" }
        ];

        manager.SetCompanies(companies);
        var result = manager.GetCompanies();

        Assert.NotNull(result);
        Assert.Equal(2, result!.Count);
        Assert.Equal("Rossi Srl", result[0].Name);
        Assert.Equal("Bianchi Spa", result[1].Name);
    }

    [Fact]
    public void GetSelectedCompanyId_ReturnsNull_WhenNotSet()
    {
        var manager = CreateManager();

        Assert.Null(manager.GetSelectedCompanyId());
    }
}
