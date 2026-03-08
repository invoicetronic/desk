using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace Desk.E2E;

[Collection("E2E")]
public class CompaniesTests(E2EFixture fixture)
{
    [Fact]
    public async Task CompaniesGrid_ShowsCompanyRows()
    {
        var page = await fixture.CreatePageAsync();
        await page.GotoAsync($"{fixture.ServerAddress}/Companies");

        var rows = page.Locator(".ag-row");
        await Assertions.Expect(rows).ToHaveCountAsync(2);
    }

    [Fact]
    public async Task CompaniesGrid_ShowsCompanyNames()
    {
        var page = await fixture.CreatePageAsync();
        await page.GotoAsync($"{fixture.ServerAddress}/Companies");

        await Assertions.Expect(page.Locator(".ag-row").First).ToBeVisibleAsync();

        await Assertions.Expect(page.Locator(".ag-cell-value", new() { HasText = "Acme S.r.l." })).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator(".ag-cell-value", new() { HasText = "Tech Solutions S.p.A." })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task CompaniesGrid_ShowsVatNumbers()
    {
        var page = await fixture.CreatePageAsync();
        await page.GotoAsync($"{fixture.ServerAddress}/Companies");

        await Assertions.Expect(page.Locator(".ag-row").First).ToBeVisibleAsync();

        await Assertions.Expect(page.Locator(".ag-cell-value", new() { HasText = "IT01234567890" })).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator(".ag-cell-value", new() { HasText = "IT09876543210" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task CompaniesGrid_ShowsFiscalCodes()
    {
        var page = await fixture.CreatePageAsync();
        await page.GotoAsync($"{fixture.ServerAddress}/Companies");

        await Assertions.Expect(page.Locator(".ag-row").First).ToBeVisibleAsync();

        await Assertions.Expect(page.Locator(".ag-cell-value", new() { HasTextRegex = new Regex("^01234567890$") })).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator(".ag-cell-value", new() { HasTextRegex = new Regex("^09876543210$") })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Companies_AddButtonOpensModal()
    {
        var page = await fixture.CreatePageAsync();
        await page.GotoAsync($"{fixture.ServerAddress}/Companies");

        // Modal should be hidden initially
        var modal = page.Locator("#companyModal");
        await Assertions.Expect(modal).Not.ToHaveClassAsync(new Regex("\\bopen\\b"));

        // Click the add button
        await page.Locator("button", new() { HasText = "+" }).ClickAsync();

        // Modal should now be open
        await Assertions.Expect(modal).ToHaveClassAsync(new Regex("\\bopen\\b"));

        // Form fields should be empty
        await Assertions.Expect(page.Locator("#companyName")).ToHaveValueAsync("");
        await Assertions.Expect(page.Locator("#companyVat")).ToHaveValueAsync("");
        await Assertions.Expect(page.Locator("#companyFiscalCode")).ToHaveValueAsync("");
    }

    [Fact]
    public async Task Companies_ModalHasCancelButton()
    {
        var page = await fixture.CreatePageAsync();
        await page.GotoAsync($"{fixture.ServerAddress}/Companies");

        // Open modal
        await page.Locator("button", new() { HasText = "+" }).ClickAsync();
        var modal = page.Locator("#companyModal");
        await Assertions.Expect(modal).ToHaveClassAsync(new Regex("\\bopen\\b"));

        // Click cancel
        await page.Locator("#companyModal .btn-brand-secondary").ClickAsync();

        // Modal should close
        await Assertions.Expect(modal).Not.ToHaveClassAsync(new Regex("\\bopen\\b"));
    }

    [Fact]
    public async Task Companies_GridHasActionButtons()
    {
        var page = await fixture.CreatePageAsync();
        await page.GotoAsync($"{fixture.ServerAddress}/Companies");

        await Assertions.Expect(page.Locator(".ag-row").First).ToBeVisibleAsync();

        // Each row should have edit and delete buttons
        await Assertions.Expect(page.Locator(".btn-icon").First).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator(".btn-icon-danger").First).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Companies_SearchFieldIsPresent()
    {
        var page = await fixture.CreatePageAsync();
        await page.GotoAsync($"{fixture.ServerAddress}/Companies");

        await Assertions.Expect(page.Locator("#filterSearch")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Companies_PageTitle()
    {
        var page = await fixture.CreatePageAsync();
        await page.GotoAsync($"{fixture.ServerAddress}/Companies");

        await Assertions.Expect(page.Locator("h1")).ToContainTextAsync("Aziende");
    }

    [Fact]
    public async Task Companies_EditButtonOpensModalWithData()
    {
        var page = await fixture.CreatePageAsync();
        await page.GotoAsync($"{fixture.ServerAddress}/Companies");

        await Assertions.Expect(page.Locator(".ag-row").First).ToBeVisibleAsync();

        // Click edit button on first row
        await page.Locator(".ag-row").First.Locator(".btn-icon").First.ClickAsync();

        // Modal should be open with pre-filled data
        var modal = page.Locator("#companyModal");
        await Assertions.Expect(modal).ToHaveClassAsync(new Regex("\\bopen\\b"));

        // Fields should be pre-filled with first company's data
        await Assertions.Expect(page.Locator("#companyName")).Not.ToHaveValueAsync("");
        await Assertions.Expect(page.Locator("#companyVat")).Not.ToHaveValueAsync("");
    }

    [Fact]
    public async Task Companies_DeleteButtonOpensConfirmModal()
    {
        var page = await fixture.CreatePageAsync();
        await page.GotoAsync($"{fixture.ServerAddress}/Companies");

        await Assertions.Expect(page.Locator(".ag-row").First).ToBeVisibleAsync();

        // Click delete button on first row
        await page.Locator(".ag-row").First.Locator(".btn-icon-danger").ClickAsync();

        // Delete confirmation modal should open
        var modal = page.Locator("#deleteModal");
        await Assertions.Expect(modal).ToHaveClassAsync(new Regex("\\bopen\\b"));

        // Should have confirm and cancel buttons
        await Assertions.Expect(modal.Locator(".btn-brand-danger")).ToBeVisibleAsync();
        await Assertions.Expect(modal.Locator(".btn-brand-secondary")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Companies_DeleteModalCancelCloses()
    {
        var page = await fixture.CreatePageAsync();
        await page.GotoAsync($"{fixture.ServerAddress}/Companies");

        await Assertions.Expect(page.Locator(".ag-row").First).ToBeVisibleAsync();

        // Open delete modal
        await page.Locator(".ag-row").First.Locator(".btn-icon-danger").ClickAsync();
        var modal = page.Locator("#deleteModal");
        await Assertions.Expect(modal).ToHaveClassAsync(new Regex("\\bopen\\b"));

        // Click cancel
        await modal.Locator(".btn-brand-secondary").ClickAsync();

        // Modal should close
        await Assertions.Expect(modal).Not.ToHaveClassAsync(new Regex("\\bopen\\b"));
    }

    [Fact]
    public async Task Companies_DeleteConfirmClosesModal()
    {
        var page = await fixture.CreatePageAsync();
        await page.GotoAsync($"{fixture.ServerAddress}/Companies");

        await Assertions.Expect(page.Locator(".ag-row").First).ToBeVisibleAsync();

        // Open delete modal
        await page.Locator(".ag-row").First.Locator(".btn-icon-danger").ClickAsync();
        var modal = page.Locator("#deleteModal");
        await Assertions.Expect(modal).ToHaveClassAsync(new Regex("\\bopen\\b"));

        // Confirm delete (fixture handler returns success)
        await modal.Locator(".btn-brand-danger").ClickAsync();

        // Modal should close after successful delete
        await Assertions.Expect(modal).Not.ToHaveClassAsync(new Regex("\\bopen\\b"));
    }

    [Fact]
    public async Task Companies_DoubleClickRowOpensEditModal()
    {
        var page = await fixture.CreatePageAsync();
        await page.GotoAsync($"{fixture.ServerAddress}/Companies");

        await Assertions.Expect(page.Locator(".ag-row").First).ToBeVisibleAsync();

        // Double-click the first row's name cell
        await page.Locator(".ag-row").First.Locator(".ag-cell-value").First.DblClickAsync();

        // Edit modal should open with data
        var modal = page.Locator("#companyModal");
        await Assertions.Expect(modal).ToHaveClassAsync(new Regex("\\bopen\\b"));
        await Assertions.Expect(page.Locator("#companyName")).Not.ToHaveValueAsync("");
    }
}
