using Desk.Models;
using Microsoft.AspNetCore.Mvc;

namespace Desk.Pages.Invoices;

public class DetailModel(ApiManager apiManager, SessionManager sessionManager, DeskConfig config)
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

            var bytes = System.Text.Encoding.UTF8.GetBytes(invoice.Payload);
            return File(bytes, "application/xml", invoice.FileName);
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
                    catch
                    {
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
