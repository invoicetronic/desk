using Microsoft.Playwright;

namespace Desk.E2E;

[Collection("E2E")]
public class SentInvoicesTests(E2EFixture fixture)
{
    [Fact]
    public async Task SentGrid_ShowsInvoiceRows()
    {
        var page = await fixture.CreatePageAsync();
        await page.GotoAsync($"{fixture.ServerAddress}/Invoices/Sent");

        var rows = page.Locator(".ag-row");
        await Assertions.Expect(rows).ToHaveCountAsync(3);
    }

    [Fact]
    public async Task SentGrid_ShowsBuyerNames()
    {
        var page = await fixture.CreatePageAsync();
        await page.GotoAsync($"{fixture.ServerAddress}/Invoices/Sent");

        await Assertions.Expect(page.Locator(".ag-row").First).ToBeVisibleAsync();

        await Assertions.Expect(page.Locator(".ag-cell-value", new() { HasText = "Cliente Uno S.r.l." })).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator(".ag-cell-value", new() { HasText = "Cliente Due S.p.A." })).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator(".ag-cell-value", new() { HasText = "Cliente Tre S.a.s." })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task SentGrid_ShowsStateBadges()
    {
        var page = await fixture.CreatePageAsync();
        await page.GotoAsync($"{fixture.ServerAddress}/Invoices/Sent");

        await Assertions.Expect(page.Locator(".ag-row").First).ToBeVisibleAsync();

        // Invoice 1 = Consegnato (success badge), Invoice 2 = Scartato (danger badge)
        await Assertions.Expect(page.Locator(".desk-badge-success")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator(".desk-badge-danger")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task SentGrid_ClickIdentifier_NavigatesToDetail()
    {
        var page = await fixture.CreatePageAsync();
        await page.GotoAsync($"{fixture.ServerAddress}/Invoices/Sent");

        await Assertions.Expect(page.Locator(".ag-row").First).ToBeVisibleAsync();

        // Click the identifier link (not the action button)
        await page.Locator(".ag-cell-value a[href*='Detail?type=send&id=1']").First.ClickAsync();
        await page.WaitForURLAsync("**/Invoices/Detail?type=send&id=1");

        // Detail page shows invoice identifier
        await Assertions.Expect(page.Locator(".desk-detail-value", new() { HasText = "IT01234567890_00001" }).First).ToBeVisibleAsync();
    }

    [Fact]
    public async Task SentDetail_ShowsSdiTimeline()
    {
        var page = await fixture.CreatePageAsync();
        await page.GotoAsync($"{fixture.ServerAddress}/Invoices/Detail?type=send&id=1");

        // SDI timeline should have 2 entries (Consegnato + Inviato)
        var timelineItems = page.Locator(".desk-timeline-item");
        await Assertions.Expect(timelineItems).ToHaveCountAsync(2);

        // First entry is the most recent (Consegnato)
        await Assertions.Expect(timelineItems.First.Locator(".desk-timeline-title")).ToContainTextAsync("Consegnat");

        // Second entry (Inviato)
        await Assertions.Expect(timelineItems.Last.Locator(".desk-timeline-title")).ToContainTextAsync("Inviat");
    }

    [Fact]
    public async Task SentDetail_ShowsInvoiceMetadata()
    {
        var page = await fixture.CreatePageAsync();
        await page.GotoAsync($"{fixture.ServerAddress}/Invoices/Detail?type=send&id=1");

        await Assertions.Expect(page.Locator(".desk-detail-value", new() { HasTextRegex = new System.Text.RegularExpressions.Regex("^IT01234567890_00001$") })).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator(".desk-detail-value", new() { HasText = "IT01234567890_00001.xml" })).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator(".desk-detail-value", new() { HasText = "FPA12" })).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator(".desk-detail-value", new() { HasText = "IT11111111111" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task SentDetail_ShowsDocuments()
    {
        var page = await fixture.CreatePageAsync();
        await page.GotoAsync($"{fixture.ServerAddress}/Invoices/Detail?type=send&id=1");

        await Assertions.Expect(page.Locator(".desk-document-item", new() { HasText = "FV-2025/001" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task SentDetail_BackButton_ReturnsToList()
    {
        var page = await fixture.CreatePageAsync();
        await page.GotoAsync($"{fixture.ServerAddress}/Invoices/Detail?type=send&id=1");

        await page.Locator("a.btn-brand[href='/Invoices/Sent']").ClickAsync();
        await page.WaitForURLAsync("**/Invoices/Sent");

        await Assertions.Expect(page.Locator("h1")).ToContainTextAsync("Fatture inviate");
    }

    [Fact]
    public async Task SentDetail_DownloadButton_IsPresent()
    {
        var page = await fixture.CreatePageAsync();
        await page.GotoAsync($"{fixture.ServerAddress}/Invoices/Detail?type=send&id=1");

        // Download XML button should be present
        var downloadLink = page.Locator("a[href*='handler=Download']");
        await Assertions.Expect(downloadLink).ToBeVisibleAsync();
    }

    [Fact]
    public async Task SentGrid_HasColumnChooserButton()
    {
        var page = await fixture.CreatePageAsync();
        await page.GotoAsync($"{fixture.ServerAddress}/Invoices/Sent");

        await Assertions.Expect(page.Locator(".ag-row").First).ToBeVisibleAsync();

        // Column chooser button should be in the toolbar
        await Assertions.Expect(page.Locator(".desk-col-chooser button")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task SentGrid_ColumnChooserOpensPanel()
    {
        var page = await fixture.CreatePageAsync();
        await page.GotoAsync($"{fixture.ServerAddress}/Invoices/Sent");

        await Assertions.Expect(page.Locator(".ag-row").First).ToBeVisibleAsync();

        // Click column chooser
        await page.Locator(".desk-col-chooser button").ClickAsync();

        // Panel should show with checkboxes
        var panel = page.Locator(".desk-col-chooser-panel");
        await Assertions.Expect(panel).ToBeVisibleAsync();
        await Assertions.Expect(panel.Locator("input[type='checkbox']").First).ToBeVisibleAsync();
    }

    [Fact]
    public async Task SentGrid_HasSearchField()
    {
        var page = await fixture.CreatePageAsync();
        await page.GotoAsync($"{fixture.ServerAddress}/Invoices/Sent");

        await Assertions.Expect(page.Locator("#filterSearch")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task SentGrid_SortableColumnHeaders()
    {
        var page = await fixture.CreatePageAsync();
        await page.GotoAsync($"{fixture.ServerAddress}/Invoices/Sent");

        await Assertions.Expect(page.Locator(".ag-row").First).ToBeVisibleAsync();

        // AG Grid headers should be present and sortable
        var headers = page.Locator(".ag-header-cell");
        await Assertions.Expect(headers.First).ToBeVisibleAsync();

        // Click a header to sort — should add sort indicator
        await page.Locator(".ag-header-cell", new() { HasText = "Identificativo" }).First.ClickAsync();
        await Assertions.Expect(page.Locator(".ag-sort-ascending-icon:visible, .ag-sort-descending-icon:visible").First).ToBeVisibleAsync();
    }
}
