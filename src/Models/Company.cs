namespace Desk.Models;

public class Company : BaseModel
{
    public int UserId { get; set; }
    public string Vat { get; set; } = null!;
    public string FiscalCode { get; set; } = null!;
    public string Name { get; set; } = null!;

    /// <summary>
    /// Whether the company is a public administration: only public administrations receive FPA12 invoices and can
    /// accept or reject them. In the Sandbox such a company receives FPA12 test invoices.
    /// </summary>
    public bool PublicAdministration { get; set; }
}
