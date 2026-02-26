namespace Desk.Models;

public class InvoiceUploadResponse
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public string? Identifier { get; set; }
    public string FileName { get; set; } = null!;
}
