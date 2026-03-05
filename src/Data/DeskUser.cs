using Microsoft.AspNetCore.Identity;

namespace Desk.Data;

public class DeskUser : IdentityUser
{
    public string? ApiKey { get; set; }
    public string? DisplayName { get; set; }
    public string? StripeCustomerId { get; set; }
    public string? SubscriptionStatus { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CompanyName { get; set; }
    public string? TaxId { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? ZipCode { get; set; }
    public string? Country { get; set; }
    public string? PecMail { get; set; }
    public string? CodiceDestinatario { get; set; }
}
