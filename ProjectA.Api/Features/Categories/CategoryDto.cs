using Dapper.Contrib.Extensions;

namespace ProjectA.Api.Features.Categories;

// Maps 1:1 onto the `categories` table for Dapper.Contrib. Property names must match column
// names exactly - Dapper.Contrib emits them verbatim and Postgres folds unquoted identifiers
// to lowercase, so these stay snake_case rather than PascalCase.
// ReSharper disable InconsistentNaming
[Table("categories")]
internal sealed class CategoryDto
{
    [Dapper.Contrib.Extensions.Key]
    public long id { get; init; }
    public string title { get; set; } = string.Empty;
    public string? description { get; set; }
}
// ReSharper restore InconsistentNaming
