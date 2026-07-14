using Dapper.Contrib.Extensions;

namespace ProjectA.Api.Features.Notes;

// Maps 1:1 onto the `notes` table for Dapper.Contrib. Property names must match column
// names exactly - Dapper.Contrib emits them verbatim and Postgres folds unquoted identifiers
// to lowercase, so these stay snake_case rather than PascalCase.
// ReSharper disable InconsistentNaming
[Table("notes")]
internal sealed class NoteDto
{
    [Dapper.Contrib.Extensions.Key]
    public long id { get; init; }
    public string? title { get; set; }
    public string? description { get; set; }
    public string? body { get; set; }
    public DateTime date_created { get; init; }
    public DateTime? date_modified { get; set; }
}
// ReSharper restore InconsistentNaming
