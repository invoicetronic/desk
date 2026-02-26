using Microsoft.AspNetCore.Identity;

namespace Desk.Data;

public class DeskUser : IdentityUser
{
    public string? ApiKey { get; set; }
    public string? DisplayName { get; set; }
}
