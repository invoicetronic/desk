namespace Desk.Models;

public abstract class BaseModel
{
    public int Id { get; set; }
    public DateTime Created { get; set; }
    public uint Version { get; set; }
}
