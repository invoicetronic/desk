namespace Desk.Models;

public abstract class Invoice : BaseModel
{
    public int UserId { get; set; }
    public int CompanyId { get; set; }
    public string Committente { get; set; } = null!;
    public string Prestatore { get; set; } = null!;
    public string? Identifier { get; set; }
    public string FileName { get; set; } = null!;
    public string Format { get; set; } = null!;
    public string Payload { get; set; } = null!;
    public DateTime? LastUpdate { get; set; }
    public DateTime? DateSent { get; set; }
    public List<DocumentData>? Documents { get; set; }
}

public class DocumentData
{
    public string Number { get; set; } = null!;
    public DateTime Date { get; set; }
}
