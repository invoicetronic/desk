using Microsoft.AspNetCore.Mvc;

namespace Desk.Pages.Upload;

public class IndexModel(ApiManager apiManager, SessionManager sessionManager, DeskConfig config)
    : AppPageModel(apiManager, sessionManager, config)
{
    private const long MaxFileSize = 5 * 1024 * 1024; // 5 MB
    private static readonly string[] AllowedExtensions = [".xml", ".p7m"];

    public async Task<IActionResult> OnPostUploadAsync(IFormFile file)
    {
        if (file.Length == 0)
            return new JsonResult(new { error = "File is empty." }) { StatusCode = 400 };

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
            return new JsonResult(new { error = $"Invalid file type '{extension}'. Allowed: .xml, .p7m" })
                { StatusCode = 400 };

        if (file.Length > MaxFileSize)
            return new JsonResult(new { error = "File exceeds maximum size of 5 MB." })
                { StatusCode = 400 };

        try
        {
            var result = await ApiManager.UploadInvoice(file);
            return new JsonResult(new { success = true, data = result });
        }
        catch (HttpRequestException ex)
        {
            return new JsonResult(new { error = ex.Message })
                { StatusCode = (int?)ex.StatusCode ?? 500 };
        }
    }
}
