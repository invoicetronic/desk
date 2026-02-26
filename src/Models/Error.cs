namespace Desk.Models;

public class Error
{
    public string Code { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string Hint { get; set; } = null!;
}
