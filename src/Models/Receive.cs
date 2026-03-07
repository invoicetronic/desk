namespace Desk.Models;

public class Receive : Invoice
{
    public string? NomePrestatore { get; set; }
    public bool IsRead { get; set; }
    public string MessageId { get; set; } = null!;
}
