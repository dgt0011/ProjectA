using System.Data;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Dapper;

namespace ProjectA.Api.Features.Blog;

// Slug rules ported from Blogifier's StringExtensions.ToSlug so newly created posts get the
// same style of URL as the migrated ones (/posts/{slug}).
internal static partial class BlogSlugs
{
    public const int MaxLength = 160;

    public static string FromTitle(string title)
    {
        var slug = title.Trim().ToLowerInvariant();
        slug = RemoveDiacritics(slug);
        slug = NonSlugCharacters().Replace(slug, string.Empty);
        slug = Whitespace().Replace(slug, "-");
        slug = RepeatedSeparators().Replace(slug, "-");
        slug = slug.Trim('-', '_');

        if (slug.Length > MaxLength)
        {
            slug = slug[..MaxLength].TrimEnd('-', '_');
        }

        return slug;
    }

    public static bool IsValid(string slug) =>
        !string.IsNullOrWhiteSpace(slug) && slug.Length <= MaxLength && ValidSlug().IsMatch(slug);

    // Pages are served by ProjectA.Blog.Web at /{slug}, but its fixed routes and static folders
    // win over that catch-all, so a page with one of these slugs could never be reached ("posts"
    // even redirected to itself forever). Posts live under /posts/ and wouldn't clash, but a
    // post can be turned into a page, so the names are reserved for both.
    private static readonly HashSet<string> Reserved = new(StringComparer.OrdinalIgnoreCase)
    {
        "posts", "categories", "search", "feed", "sitemap", "error", "admin", "data", "img", "themes"
    };

    public static bool IsReserved(string slug) => Reserved.Contains(slug);

    // The slug existence checks run before the write, so a concurrent save can still take the
    // slug in between; the unique index then rejects the write and this turns that into the
    // same validation problem the check would have returned.
    public const string SlugIndexName = "idx_blog_posts_slug";

    public static bool IsSlugConflict(Npgsql.PostgresException ex) =>
        ex.SqlState == Npgsql.PostgresErrorCodes.UniqueViolation && ex.ConstraintName == SlugIndexName;

    public static Microsoft.AspNetCore.Http.HttpResults.ValidationProblem ConflictProblem(string slug) =>
        Microsoft.AspNetCore.Http.TypedResults.ValidationProblem(new Dictionary<string, string[]>
        {
            ["Slug"] = [$"The slug '{slug}' is already used by another post."]
        });

    // Appends 2, 3, ... (as Blogifier did) until the slug is free. excludePostId lets an edit
    // keep its own slug without it counting as a clash.
    public static async Task<string> MakeUniqueAsync(
        IDbConnection connection, string baseSlug, long? excludePostId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(baseSlug))
        {
            baseSlug = "post";
        }

        var candidate = baseSlug;
        for (var suffix = 2;
             IsReserved(candidate) || await ExistsAsync(connection, candidate, excludePostId, cancellationToken);
             suffix++)
        {
            var suffixText = suffix.ToString(CultureInfo.InvariantCulture);
            var stem = baseSlug.Length + suffixText.Length > MaxLength
                ? baseSlug[..(MaxLength - suffixText.Length)]
                : baseSlug;
            candidate = stem + suffixText;
        }

        return candidate;
    }

    public static Task<bool> ExistsAsync(
        IDbConnection connection, string slug, long? excludePostId, CancellationToken cancellationToken) =>
        connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS (SELECT 1 FROM blog_posts WHERE slug = @Slug AND (@ExcludeId::bigint IS NULL OR id <> @ExcludeId));",
            new { Slug = slug, ExcludeId = excludePostId },
            cancellationToken: cancellationToken));

    private static string RemoveDiacritics(string text)
    {
        var normalized = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category is not (UnicodeCategory.NonSpacingMark
                or UnicodeCategory.InitialQuotePunctuation
                or UnicodeCategory.FinalQuotePunctuation))
            {
                builder.Append(c);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    [GeneratedRegex(@"[^\p{L}\p{N}\s_-]")]
    private static partial Regex NonSlugCharacters();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    [GeneratedRegex(@"[-_]{2,}")]
    private static partial Regex RepeatedSeparators();

    [GeneratedRegex(@"^[\p{L}\p{N}]+(?:[-_][\p{L}\p{N}]+)*$")]
    private static partial Regex ValidSlug();
}
