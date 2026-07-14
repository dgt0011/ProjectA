using Dapper.Contrib.Extensions;

namespace ProjectA.Api.Features.BookmarkTypes;

// Maps 1:1 onto the `bookmark_types` table for Dapper.Contrib. Property names must match
// column names exactly - Dapper.Contrib emits them verbatim and Postgres folds unquoted
// identifiers to lowercase, so these stay snake_case rather than PascalCase.
// ReSharper disable InconsistentNaming
[Table("bookmark_types")]
internal sealed class BookmarkTypeDto
{
    [Dapper.Contrib.Extensions.Key]
    public long id { get; init; }
    public string title { get; set; } = string.Empty;
    public string? color { get; set; }
    public byte[]? icon { get; set; }
    public string? icon_content_type { get; set; }
}
// ReSharper restore InconsistentNaming
