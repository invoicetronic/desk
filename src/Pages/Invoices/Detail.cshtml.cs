using Desk.Models;
using Microsoft.AspNetCore.Mvc;

namespace Desk.Pages.Invoices;

public class DetailModel(ApiManager apiManager, SessionManager sessionManager, DeskConfig config, ILogger<DetailModel> logger)
    : AppPageModel(apiManager, sessionManager, config)
{
    public Send? SendInvoice { get; set; }
    public Receive? ReceiveInvoice { get; set; }
    public List<Update>? Updates { get; set; }
    public string InvoiceType { get; set; } = "";
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetDownloadAsync(string type, int id)
    {
        try
        {
            Invoice? invoice = type switch
            {
                "send" => await ApiManager.Get<Send>(id, "include_payload=true"),
                "receive" => await ApiManager.Get<Receive>(id, "include_payload=true"),
                _ => null
            };

            if (invoice is null)
                return NotFound();

            // Base64 payloads (signed .p7m, XML in an encoding other than UTF-8) carry the original file bytes.
            var bytes = invoice.Encoding == PayloadEncoding.Base64
                ? Convert.FromBase64String(invoice.Payload)
                : System.Text.Encoding.UTF8.GetBytes(invoice.Payload);
            var contentType = invoice.FileName.EndsWith(".p7m", StringComparison.OrdinalIgnoreCase)
                ? "application/pkcs7-mime"
                : "application/xml";
            return File(bytes, contentType, invoice.FileName);
        }
        catch (HttpRequestException ex)
        {
            return new JsonResult(new { error = ex.Message })
                { StatusCode = (int?)ex.StatusCode ?? 500 };
        }
    }

    public async Task<IActionResult> OnGetAsync(string type, int id)
    {
        InvoiceType = type;

        try
        {
            switch (type)
            {
                case "send":
                    SendInvoice = await ApiManager.Get<Send>(id);
                    if (SendInvoice is null) return NotFound();

                    // Load SDI updates for sent invoices
                    try
                    {
                        var (updates, _) = await ApiManager.List<Update>(
                            extraQuery: $"send_id={id}", sort: "-last_update");
                        Updates = updates ?? [];
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to load SDI updates for send invoice {Id}", id);
                        Updates = [];
                    }
                    break;

                case "receive":
                    ReceiveInvoice = await ApiManager.Get<Receive>(id);
                    if (ReceiveInvoice is null) return NotFound();
                    break;

                default:
                    return NotFound();
            }
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return NotFound();
        }
        catch (HttpRequestException ex)
        {
            ErrorMessage = ex.Message;
            return Page();
        }

        return Page();
    }
}
