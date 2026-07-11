using Dapper.Contrib.Extensions;

namespace ProjectA.Api.Features.Users;

// Maps 1:1 onto the `users` table for Dapper.Contrib. Property names must match column
// names exactly - Dapper.Contrib emits them verbatim and Postgres folds unquoted identifiers
// to lowercase, so these stay snake_case rather than PascalCase.
// ReSharper disable InconsistentNaming
[Table("users")]
internal sealed class UserDto
{
    [Dapper.Contrib.Extensions.Key]
    public long id { get; init; }
    public string username { get; set; } = string.Empty;
    public string password_hash { get; set; } = string.Empty;
    public DateTime date_created { get; init; }
    public DateTime? date_modified { get; set; }
}
// ReSharper restore InconsistentNaming
