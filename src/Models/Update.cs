namespace Desk.Models;

public class Update : BaseModel
{
    public int UserId { get; set; }
    public int CompanyId { get; set; }
    public int SendId { get; set; }
    public DateTime LastUpdate { get; set; }
    public State State { get; set; }
    public string? Description { get; set; }
    public string? MessageId { get; set; }
    public List<Error>? Errors { get; set; }
    public bool IsRead { get; set; }
}
