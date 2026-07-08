using System.ComponentModel.DataAnnotations;
using Dapper.Contrib.Extensions;

namespace ProjectA.Api.Features.ToDo;

// Maps 1:1 onto the `todos` table for Dapper.Contrib. Property names must match column
// names exactly - Dapper.Contrib emits them verbatim and Postgres folds unquoted
// identifiers to lowercase, so these stay snake_case rather than PascalCase.
//
// This is shared across the ToDo feature's slices (unlike request/response records, which
// each slice owns privately) because it mirrors a single physical table - duplicating it
// per slice would just create five copies that could drift out of sync with the schema.
// ReSharper disable InconsistentNaming
[Table("todos")]
internal sealed class ToDoEntity
{
    [Key]
    public long id { get; set; }
    public string title { get; set; } = string.Empty;
    public bool actioned { get; set; }
    public string category { get; set; } = string.Empty;
    public string? description { get; set; }
    public DateTime? date_created { get; set; }
    public DateTime? date_modified { get; set; }
}
// ReSharper restore InconsistentNaming
