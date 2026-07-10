using Dapper.Contrib.Extensions;

namespace ProjectA.Api.Features.Projects;

// Maps 1:1 onto the `projects` table for Dapper.Contrib. Property names must match column
// names exactly - Dapper.Contrib emits them verbatim and Postgres folds unquoted identifiers
// to lowercase, so these stay snake_case rather than PascalCase.
//
// start_date is DateOnly, not DateTime: Postgres `date` maps to System.DateOnly by default in
// modern Npgsql, and nothing in this codebase opts into the legacy DateTime mapping.
// ReSharper disable InconsistentNaming
[Table("projects")]
internal sealed class ProjectDto
{
    [Dapper.Contrib.Extensions.Key]
    public long id { get; init; }
    public string title { get; set; } = string.Empty;
    public string? description { get; set; }
    public DateTime start_date { get; set; }
}
// ReSharper restore InconsistentNaming
