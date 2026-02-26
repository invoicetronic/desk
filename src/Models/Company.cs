namespace Desk.Models;

public class Company : BaseModel
{
    public int UserId { get; set; }
    public string Vat { get; set; } = null!;
    public string FiscalCode { get; set; } = null!;
    public string Name { get; set; } = null!;
}
