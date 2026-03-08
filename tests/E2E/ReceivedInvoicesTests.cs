using Microsoft.Playwright;

namespace Desk.E2E;

[Collection("E2E")]
public class ReceivedInvoicesTests(E2EFixture fixture)
{
    [Fact]
    public async Task ReceivedGrid_ShowsInvoiceRows()
    {
        var page = await fixture.CreatePageAsync();
        await page.GotoAsync($"{fixture.ServerAddress}/Invoices/Received");

        // Wait for AG Grid to load data
        var rows = page.Locator(".ag-row");
        await Assertions.Expect(rows).ToHaveCountAsync(3);
    }

    [Fact]
    public async Task ReceivedGrid_ShowsSupplierNames()
    {
        var page = await fixture.CreatePageAsync();
        await page.GotoAsync($"{fixture.ServerAddress}/Invoices/Received");

        // Wait for grid to populate
        await Assertions.Expect(page.Locator(".ag-row").First).ToBeVisibleAsync();

        // Supplier names should be visible in the grid
        await Assertions.Expect(page.Locator(".ag-cell-value", new() { HasText = "Fornitore Alfa S.r.l." })).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator(".ag-cell-value", new() { HasText = "Fornitore Beta S.p.A." })).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator(".ag-cell-value", new() { HasText = "Fornitore Gamma S.a.s." })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task ReceivedGrid_ShowsIdentifierAsLink()
    {
        var page = await fixture.CreatePageAsync();
        await page.GotoAsync($"{fixture.ServerAddress}/Invoices/Received");

        await Assertions.Expect(page.Locator(".ag-row").First).ToBeVisibleAsync();

        // Identifier column should contain links to detail page
        var link = page.Locator("a[href*='Detail?type=receive']").First;
        await Assertions.Expect(link).ToBeVisibleAsync();
    }

    [Fact]
    public async Task ReceivedGrid_ClickIdentifier_NavigatesToDetail()
    {
        var page = await fixture.CreatePageAsync();
        await page.GotoAsync($"{fixture.ServerAddress}/Invoices/Received");

        await Assertions.Expect(page.Locator(".ag-row").First).ToBeVisibleAsync();

        // Click first invoice identifier link (not the action icon)
        await page.Locator(".ag-cell-value a[href*='Detail?type=receive&id=1']").First.ClickAsync();
        await page.WaitForURLAsync("**/Invoices/Detail?type=receive&id=1");

        // Detail page should show invoice metadata
        await Assertions.Expect(page.Locator(".desk-detail-value", new() { HasTextRegex = new System.Text.RegularExpressions.Regex("^IT44444444444_00010$") })).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator(".desk-detail-value", new() { HasText = "FPA12" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task ReceivedDetail_ShowsReadBadge()
    {
        var page = await fixture.CreatePageAsync();
        await page.GotoAsync($"{fixture.ServerAddress}/Invoices/Detail?type=receive&id=1");

        // Invoice 1 is read — should show the "read" badge
        await Assertions.Expect(page.Locator(".desk-badge-success")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task ReceivedDetail_ShowsDocuments()
    {
        var page = await fixture.CreatePageAsync();
        await page.GotoAsync($"{fixture.ServerAddress}/Invoices/Detail?type=receive&id=1");

        // Document number should be visible
        await Assertions.Expect(page.Locator(".desk-document-item", new() { HasText = "FA-2025/050" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task ReceivedDetail_BackButton_ReturnsToList()
    {
        var page = await fixture.CreatePageAsync();
        await page.GotoAsync($"{fixture.ServerAddress}/Invoices/Detail?type=receive&id=1");

        await page.Locator("a.btn-brand[href='/Invoices/Received']").ClickAsync();
        await page.WaitForURLAsync("**/Invoices/Received");

        await Assertions.Expect(page.Locator("h1")).ToContainTextAsync("Fatture ricevute");
    }

    [Fact]
    public async Task ReceivedDetail_ShowsMessageId()
    {
        var page = await fixture.CreatePageAsync();
        await page.GotoAsync($"{fixture.ServerAddress}/Invoices/Detail?type=receive&id=1");

        await Assertions.Expect(page.Locator(".desk-detail-value", new() { HasText = "MSG-RCV-001" })).ToBeVisibleAsync();
    }
}
