using Microsoft.Playwright;

namespace Desk.E2E;

[Collection("E2E")]
public class ExportTests(E2EFixture fixture)
{
    [Fact]
    public async Task Export_PageTitle()
    {
        var page = await fixture.CreatePageAsync();
        await page.GotoAsync($"{fixture.ServerAddress}/Export");

        await Assertions.Expect(page.Locator("h1")).ToContainTextAsync("Esporta fatture");
    }

    [Fact]
    public async Task Export_TypeSelectorHasThreeOptions()
    {
        var page = await fixture.CreatePageAsync();
        await page.GotoAsync($"{fixture.ServerAddress}/Export");

        var options = page.Locator("#exportType option");
        await Assertions.Expect(options).ToHaveCountAsync(3);
    }

    [Fact]
    public async Task Export_DefaultTypeIsBoth()
    {
        var page = await fixture.CreatePageAsync();
        await page.GotoAsync($"{fixture.ServerAddress}/Export");

        await Assertions.Expect(page.Locator("#exportType")).ToHaveValueAsync("both");
    }

    [Fact]
    public async Task Export_PeriodSelectorHasThreeOptions()
    {
        var page = await fixture.CreatePageAsync();
        await page.GotoAsync($"{fixture.ServerAddress}/Export");

        var options = page.Locator("#exportPeriod option");
        await Assertions.Expect(options).ToHaveCountAsync(3);
    }

    [Fact]
    public async Task Export_MonthFieldsVisibleByDefault()
    {
        var page = await fixture.CreatePageAsync();
        await page.GotoAsync($"{fixture.ServerAddress}/Export");

        // Month fields visible, quarter and range hidden
        await Assertions.Expect(page.Locator("#monthFields")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("#quarterFields")).ToBeHiddenAsync();
        await Assertions.Expect(page.Locator("#rangeFields")).ToBeHiddenAsync();
    }

    [Fact]
    public async Task Export_SwitchToQuarterShowsQuarterFields()
    {
        var page = await fixture.CreatePageAsync();
        await page.GotoAsync($"{fixture.ServerAddress}/Export");

        await page.Locator("#exportPeriod").SelectOptionAsync("quarter");

        await Assertions.Expect(page.Locator("#monthFields")).ToBeHiddenAsync();
        await Assertions.Expect(page.Locator("#quarterFields")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("#rangeFields")).ToBeHiddenAsync();
    }

    [Fact]
    public async Task Export_SwitchToDateRangeShowsDateFields()
    {
        var page = await fixture.CreatePageAsync();
        await page.GotoAsync($"{fixture.ServerAddress}/Export");

        await page.Locator("#exportPeriod").SelectOptionAsync("range");

        await Assertions.Expect(page.Locator("#monthFields")).ToBeHiddenAsync();
        await Assertions.Expect(page.Locator("#quarterFields")).ToBeHiddenAsync();
        await Assertions.Expect(page.Locator("#rangeFields")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Export_DownloadButtonIsPresent()
    {
        var page = await fixture.CreatePageAsync();
        await page.GotoAsync($"{fixture.ServerAddress}/Export");

        await Assertions.Expect(page.Locator("#downloadBtn")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Export_QuarterFieldHasFourOptions()
    {
        var page = await fixture.CreatePageAsync();
        await page.GotoAsync($"{fixture.ServerAddress}/Export");

        await page.Locator("#exportPeriod").SelectOptionAsync("quarter");

        var options = page.Locator("#exportQuarter option");
        await Assertions.Expect(options).ToHaveCountAsync(4);
    }
}
