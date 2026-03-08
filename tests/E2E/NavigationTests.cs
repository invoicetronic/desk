using Microsoft.Playwright;

namespace Desk.E2E;

[Collection("E2E")]
public class NavigationTests(E2EFixture fixture)
{
    [Fact]
    public async Task SubNav_HasAllLinks()
    {
        var page = await fixture.CreatePageAsync();
        await page.GotoAsync(fixture.ServerAddress);

        var nav = page.Locator(".desk-subnav");
        await Assertions.Expect(nav.Locator("a[href='/']")).ToBeVisibleAsync();
        await Assertions.Expect(nav.Locator("a[href='/Invoices/Sent']")).ToBeVisibleAsync();
        await Assertions.Expect(nav.Locator("a[href='/Invoices/Received']")).ToBeVisibleAsync();
        await Assertions.Expect(nav.Locator("a[href='/Upload']")).ToBeVisibleAsync();
        await Assertions.Expect(nav.Locator("a[href='/Export']")).ToBeVisibleAsync();
        await Assertions.Expect(nav.Locator("a[href='/Companies']")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task SubNav_HomeIsActiveOnDashboard()
    {
        var page = await fixture.CreatePageAsync();
        await page.GotoAsync(fixture.ServerAddress);

        await Assertions.Expect(page.Locator(".desk-subnav a[href='/']")).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("\\bactive\\b"));
    }

    [Fact]
    public async Task SubNav_NavigateToCompanies()
    {
        var page = await fixture.CreatePageAsync();
        await page.GotoAsync(fixture.ServerAddress);

        await page.Locator(".desk-subnav a[href='/Companies']").ClickAsync();
        await page.WaitForURLAsync("**/Companies");

        await Assertions.Expect(page.Locator("h1")).ToContainTextAsync("Aziende");
    }

    [Fact]
    public async Task SubNav_NavigateToUpload()
    {
        var page = await fixture.CreatePageAsync();
        await page.GotoAsync(fixture.ServerAddress);

        await page.Locator(".desk-subnav a[href='/Upload']").ClickAsync();
        await page.WaitForURLAsync("**/Upload");

        await Assertions.Expect(page.Locator("h1")).ToContainTextAsync("Invio fatture attive");
    }

    [Fact]
    public async Task SubNav_NavigateToExport()
    {
        var page = await fixture.CreatePageAsync();
        await page.GotoAsync(fixture.ServerAddress);

        await page.Locator(".desk-subnav a[href='/Export']").ClickAsync();
        await page.WaitForURLAsync("**/Export");

        await Assertions.Expect(page.Locator("h1")).ToContainTextAsync("Esporta fatture");
    }

    [Fact]
    public async Task SubNav_CompaniesPageHasActiveLink()
    {
        var page = await fixture.CreatePageAsync();
        await page.GotoAsync($"{fixture.ServerAddress}/Companies");

        await Assertions.Expect(page.Locator(".desk-subnav a[href='/Companies']")).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("\\bactive\\b"));
    }

    [Fact]
    public async Task Navbar_LogoLinksToHome()
    {
        var page = await fixture.CreatePageAsync();
        await page.GotoAsync($"{fixture.ServerAddress}/Companies");

        await page.Locator(".desk-navbar-brand").ClickAsync();
        await page.WaitForURLAsync($"{fixture.ServerAddress}/");

        await Assertions.Expect(page.Locator("h1")).ToContainTextAsync("Dashboard");
    }

    [Fact]
    public async Task CompanySelector_IsVisibleInNavbar()
    {
        var page = await fixture.CreatePageAsync();
        await page.GotoAsync(fixture.ServerAddress);

        // Company selector should be visible (2 companies in fixtures)
        await Assertions.Expect(page.Locator("#companySelector")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task CompanySelector_DropdownOpensOnClick()
    {
        var page = await fixture.CreatePageAsync();
        await page.GotoAsync(fixture.ServerAddress);

        // Click the company selector button
        await page.Locator(".desk-company-btn").ClickAsync();

        // Dropdown should show company names
        var dropdown = page.Locator("#companyDropdown");
        await Assertions.Expect(dropdown).ToBeVisibleAsync();
        await Assertions.Expect(dropdown.Locator("a", new() { HasText = "Acme S.r.l." })).ToBeVisibleAsync();
        await Assertions.Expect(dropdown.Locator("a", new() { HasText = "Tech Solutions S.p.A." })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task CompanySelector_HasAllCompaniesOption()
    {
        var page = await fixture.CreatePageAsync();
        await page.GotoAsync(fixture.ServerAddress);

        await page.Locator(".desk-company-btn").ClickAsync();

        // "All" option should be present
        var allOption = page.Locator("#companyDropdown a[data-all]");
        await Assertions.Expect(allOption).ToBeVisibleAsync();
    }
}
