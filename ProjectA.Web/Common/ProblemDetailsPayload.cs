namespace ProjectA.Web.Common;

// Mirrors the application/problem+json shape ProjectA.Api returns for both
// TypedResults.Problem (404/409, uses Detail) and TypedResults.ValidationProblem
// (400, uses Errors). Not every endpoint populates every field.
public sealed class ProblemDetailsPayload
{
    public string? Type { get; init; }
    public string? Title { get; init; }
    
    public int? Status { get; init; }
    public string? Detail { get; init; }
    
    public string? Instance { get; init; }
    public Dictionary<string, string[]>? Errors { get; init; }
}
