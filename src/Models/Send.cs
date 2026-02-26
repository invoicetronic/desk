namespace Desk.Models;

public class Send : Invoice
{
    public Dictionary<string, string>? MetaData { get; set; }
}
