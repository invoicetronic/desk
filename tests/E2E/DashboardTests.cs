using Microsoft.Playwright;

namespace Desk.E2E;

[Collection("E2E")]
public class DashboardTests(E2EFixture fixture)
{
    [Fact]
    public async Task Dashboard_ShowsSummaryCards()
    {
        var page = await fixture.CreatePageAsync();
        await page.GotoAsync(fixture.ServerAddress);

        // Dashboard should load without errors
        await Assertions.Expect(page.Locator("h1")).ToContainTextAsync("Dashboard");

        // Summary cards with counts should be visible
        var cards = page.Locator(".desk-card-value");
        await Assertions.Expect(cards.First).ToBeVisibleAsync();

        // Sent count = 3, Received count = 3
        await Assertions.Expect(page.Locator("a[href='/Invoices/Sent'] .desk-card-value")).ToHaveTextAsync("3");
        await Assertions.Expect(page.Locator("a[href='/Invoices/Received'] .desk-card-value")).ToHaveTextAsync("3");
    }

    [Fact]
    public async Task Dashboard_ShowsRecentUpdatesTimeline()
    {
        var page = await fixture.CreatePageAsync();
        await page.GotoAsync(fixture.ServerAddress);

        // SDI timeline should be visible with update entries
        var timeline = page.Locator(".desk-timeline-item");
        await Assertions.Expect(timeline).ToHaveCountAsync(4);

        // First entry should be the most recent update (Consegnato for invoice 1)
        await Assertions.Expect(timeline.First.Locator(".desk-timeline-title")).ToContainTextAsync("Consegnat");
    }

    [Fact]
    public async Task Dashboard_SentCardNavigatesToSentPage()
    {
        var page = await fixture.CreatePageAsync();
        await page.GotoAsync(fixture.ServerAddress);

        await page.Locator("a.desk-card[href='/Invoices/Sent']").ClickAsync();
        await page.WaitForURLAsync("**/Invoices/Sent");

        await Assertions.Expect(page.Locator("h1")).ToContainTextAsync("Fatture inviate");
    }

    [Fact]
    public async Task Dashboard_ReceivedCardNavigatesToReceivedPage()
    {
        var page = await fixture.CreatePageAsync();
        await page.GotoAsync(fixture.ServerAddress);

        await page.Locator("a.desk-card[href='/Invoices/Received']").ClickAsync();
        await page.WaitForURLAsync("**/Invoices/Received");

        await Assertions.Expect(page.Locator("h1")).ToContainTextAsync("Fatture ricevute");
    }

    [Fact]
    public async Task Dashboard_ShowsOperationsLeftCounter()
    {
        var page = await fixture.CreatePageAsync();
        await page.GotoAsync(fixture.ServerAddress);

        // status.json has operation_left: 950
        await Assertions.Expect(page.Locator(".desk-card-value", new() { HasText = "950" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Dashboard_ShowsSignaturesLeftCounter()
    {
        var page = await fixture.CreatePageAsync();
        await page.GotoAsync(fixture.ServerAddress);

        // status.json has signature_left: 100
        await Assertions.Expect(page.Locator(".desk-card-value", new() { HasText = "100" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Dashboard_ShowsUnreadBadge()
    {
        var page = await fixture.CreatePageAsync();
        await page.GotoAsync(fixture.ServerAddress);

        // received-invoices.json has 2 unread invoices
        var unreadBadge = page.Locator("a[href='/Invoices/Received'] .desk-badge-info");
        await Assertions.Expect(unreadBadge).ToBeVisibleAsync();
        await Assertions.Expect(unreadBadge).ToContainTextAsync("2");
    }

    [Fact]
    public async Task Dashboard_TimelineEntryLinksToDetail()
    {
        var page = await fixture.CreatePageAsync();
        await page.GotoAsync(fixture.ServerAddress);

        // Timeline entries should link to invoice detail
        var link = page.Locator(".desk-timeline-title a[href*='Detail?type=send']").First;
        await Assertions.Expect(link).ToBeVisibleAsync();
    }
}
