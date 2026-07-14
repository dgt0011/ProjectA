using Dapper.Contrib.Extensions;

namespace ProjectA.Api.Features.Bookmarks;

// Maps 1:1 onto the `bookmarks` table for Dapper.Contrib. Property names must match column
// names exactly - Dapper.Contrib emits them verbatim and Postgres folds unquoted identifiers
// to lowercase, so these stay snake_case rather than PascalCase.
// ReSharper disable InconsistentNaming
[Table("bookmarks")]
internal sealed class BookmarkDto
{
    [Dapper.Contrib.Extensions.Key]
    public long id { get; init; }
    public string url { get; set; } = string.Empty;
    public string? title { get; set; }
    public string? description { get; set; }
    public short rating { get; set; }
    public long? bookmark_type_id { get; set; }
    public DateTime date_created { get; init; }
    public DateTime? date_modified { get; set; }
}
// ReSharper restore InconsistentNaming
