namespace Desk.Models;

public class Receive : Invoice
{
    public bool IsRead { get; set; }
    public string MessageId { get; set; } = null!;
}
