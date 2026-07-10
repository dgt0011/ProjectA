namespace ProjectA.Web.Common;

// Mirrors the application/problem+json shape ProjectA.Api returns for both
// TypedResults.Problem (404/409, uses Detail) and TypedResults.ValidationProblem
// (400, uses Errors). Not every endpoint populates every field.
public sealed class ProblemDetailsPayload
{
    public string? Type { get; set; }
    public string? Title { get; set; }
    public int? Status { get; set; }
    public string? Detail { get; set; }
    public string? Instance { get; set; }
    public Dictionary<string, string[]>? Errors { get; set; }
}
