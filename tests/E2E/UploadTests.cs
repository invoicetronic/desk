using Microsoft.Playwright;

namespace Desk.E2E;

[Collection("E2E")]
public class UploadTests(E2EFixture fixture)
{
    [Fact]
    public async Task Upload_PageTitle()
    {
        var page = await fixture.CreatePageAsync();
        await page.GotoAsync($"{fixture.ServerAddress}/Upload");

        await Assertions.Expect(page.Locator("h1")).ToContainTextAsync("Invio fatture attive");
    }

    [Fact]
    public async Task Upload_DropZoneIsVisible()
    {
        var page = await fixture.CreatePageAsync();
        await page.GotoAsync($"{fixture.ServerAddress}/Upload");

        await Assertions.Expect(page.Locator("#dropZone")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Upload_FileInputAcceptsXmlAndP7m()
    {
        var page = await fixture.CreatePageAsync();
        await page.GotoAsync($"{fixture.ServerAddress}/Upload");

        var input = page.Locator("#fileInput");
        await Assertions.Expect(input).ToHaveAttributeAsync("accept", ".xml,.p7m");
    }

    [Fact]
    public async Task Upload_FileInputIsHidden()
    {
        var page = await fixture.CreatePageAsync();
        await page.GotoAsync($"{fixture.ServerAddress}/Upload");

        // The file input should be present but visually hidden
        await Assertions.Expect(page.Locator("#fileInput")).ToBeHiddenAsync();
    }

    [Fact]
    public async Task Upload_UploadListIsEmpty()
    {
        var page = await fixture.CreatePageAsync();
        await page.GotoAsync($"{fixture.ServerAddress}/Upload");

        // Upload list should be empty initially
        var items = page.Locator(".desk-upload-item");
        await Assertions.Expect(items).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task Upload_FileUploadShowsSuccessStatus()
    {
        var page = await fixture.CreatePageAsync();
        await page.GotoAsync($"{fixture.ServerAddress}/Upload");

        // Create a minimal XML file and upload via the hidden file input
        var fileInput = page.Locator("#fileInput");
        await fileInput.SetInputFilesAsync(new FilePayload
        {
            Name = "test-invoice.xml",
            MimeType = "application/xml",
            Buffer = "<Invoice />"u8.ToArray()
        });

        // A file item should appear in the upload list
        var item = page.Locator(".desk-upload-item");
        await Assertions.Expect(item).ToHaveCountAsync(1);
        await Assertions.Expect(item.Locator(".desk-upload-item-name")).ToHaveTextAsync("test-invoice.xml");

        // Status should show success (the fixture handler returns OK for send/file)
        var badge = item.Locator(".desk-badge-success");
        await Assertions.Expect(badge).ToBeVisibleAsync(new() { Timeout = 5000 });
    }
}
