namespace Desk.Models;

public class Send : Invoice
{
    public string? NomeCommittente { get; set; }
    public Dictionary<string, string>? MetaData { get; set; }
}
