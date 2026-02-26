namespace Desk.Models;

public class ApiErrorResponse
{
    public ProblemDetails? ProblemDetails { get; set; }
    public string? ContentType { get; set; }
    public int? StatusCode { get; set; }
}

public class ProblemDetails
{
    public string? Type { get; set; }
    public string? Title { get; set; }
    public int? Status { get; set; }
    public string? Detail { get; set; }
}
