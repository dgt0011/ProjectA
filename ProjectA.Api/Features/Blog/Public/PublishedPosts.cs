using System.Data;
using Dapper;

namespace ProjectA.Api.Features.Blog.Public;

// The public (anonymous) blog endpoints all return posts in the same list-item shape and
// share the same "what counts as visible" rules, so - like NoteCategoryLinks for notes - that
// shape and query live here rather than being re-declared in every slice.
public sealed record BlogCategoryRef(long Id, string Title);

public sealed record PublishedPostSummary(
    long Id,
    short PostType,
    string Title,
    string Slug,
    string Description,
    string? Cover,
    DateTime PublishedAt,
    bool IsFeatured,
    int PostViews,
    string AuthorName,
    string? AuthorAvatar,
    IReadOnlyList<BlogCategoryRef> Categories);

// One page of a public listing (home page, category page, search results). HasOlder/HasNewer
// drive the theme's "Previous"/"Next" pagination links exactly as Blogifier's Pager did.
public sealed record PublishedPostPage(
    IReadOnlyList<PublishedPostSummary> Items,
    int Page,
    int ItemsPerPage,
    int TotalItems,
    bool HasOlder,
    bool HasNewer)
{
    public static PublishedPostPage Create(IReadOnlyList<PublishedPostSummary> items, int page, int itemsPerPage, int totalItems) =>
        new(items, page, itemsPerPage, totalItems, totalItems > page * itemsPerPage, page > 1);
}

internal static class PublishedPosts
{
    // Far beyond any real page count, and small enough that (page - 1) * itemsPerPage (at most
    // 100 per page) can't overflow an int and turn into a negative OFFSET.
    public const int MaxPage = 100_000;

    public static int NormalizePage(int? page) => page is null or < 1 ? 1 : Math.Min(page.Value, MaxPage);

    public static async Task<int> GetItemsPerPageAsync(IDbConnection connection, CancellationToken cancellationToken)
    {
        var value = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
            "SELECT items_per_page FROM blog_settings ORDER BY id LIMIT 1;",
            cancellationToken: cancellationToken));
        return value is > 0 ? value.Value : 10;
    }

    // Only published posts are ever visible through the public endpoints; drafts are only
    // reachable through the authenticated /api/blog/posts admin endpoints. Deliberately no
    // "published_at <= now()" check: there is no scheduled publishing, and published_at is
    // stamped from the API host's clock, so comparing it to the database server's clock would
    // hide a just-published post whenever the two clocks drift apart.
    public const string VisibleFilter = "p.published_at IS NOT NULL";

    private const string SelectColumns =
        """
        SELECT p.id, p.post_type, p.title, p.slug, p.description, p.content, p.cover,
               p.published_at, p.is_featured, p.post_views,
               -- Display name only: never fall back to the login username, which would publish
               -- half of an admin login on every post. Blog.Web shows the blog instead.
               COALESCE(NULLIF(btrim(u.display_name), ''), '') AS author_name,
               u.avatar AS author_avatar
        FROM blog_posts p
        LEFT JOIN users u ON u.id = p.author_id
        """;

    // ReSharper disable InconsistentNaming
    internal sealed class Row
    {
        public long id { get; init; }
        public short post_type { get; init; }
        public string title { get; init; } = string.Empty;
        public string slug { get; init; } = string.Empty;
        public string description { get; init; } = string.Empty;
        public string content { get; init; } = string.Empty;
        public string? cover { get; init; }
        public DateTime published_at { get; init; }
        public bool is_featured { get; init; }
        public int post_views { get; init; }
        public string author_name { get; init; } = string.Empty;
        public string? author_avatar { get; init; }
    }
    // ReSharper restore InconsistentNaming

    public static async Task<List<Row>> QueryAsync(
        IDbConnection connection,
        string whereClause,
        string orderAndLimit,
        object? parameters,
        CancellationToken cancellationToken)
    {
        var sql = $"{SelectColumns} WHERE {VisibleFilter} AND ({whereClause}) {orderAndLimit};";
        var rows = await connection.QueryAsync<Row>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public static async Task<List<PublishedPostSummary>> ToSummariesAsync(
        IDbConnection connection, IReadOnlyCollection<Row> rows, CancellationToken cancellationToken)
    {
        var categories = await BlogPostCategoryLinks.GetForPostsAsync(
            connection, rows.Select(r => r.id).ToList(), cancellationToken);

        return rows.Select(r => ToSummary(r, categories[r.id])).ToList();
    }

    public static PublishedPostSummary ToSummary(Row row, IEnumerable<BlogPostCategoryLinks.PostCategory> categories) =>
        new(
            row.id,
            row.post_type,
            row.title,
            row.slug,
            row.description,
            row.cover,
            row.published_at,
            row.is_featured,
            row.post_views,
            row.author_name,
            row.author_avatar,
            categories.Select(c => new BlogCategoryRef(c.Id, c.Title)).ToList());
}

// Blogifier's search ranking (PostProvider.Search), kept so search results and "related
// posts" come back in the same order readers are used to: category name match = 10,
// each title hit = 10, description hit = 3, content hit = 1. Terms shorter than 4 characters
// only count until something has already matched, which stops "a"/"the" dominating.
//
// The search box is anonymous, so the work per request is bounded: only the first MaxTerms
// distinct words of the first MaxTermLength characters are used, and matches are counted with an
// ordinal IndexOf scan (no per-term Regex construction).
internal static class BlogSearchRanking
{
    public const int MaxTermLength = 200;
    public const int MaxTerms = 10;

    public static List<(PublishedPosts.Row Row, int Rank)> Rank(
        IEnumerable<PublishedPosts.Row> rows,
        ILookup<long, BlogPostCategoryLinks.PostCategory> categories,
        string term,
        CancellationToken cancellationToken = default)
    {
        if (term.Length > MaxTermLength)
        {
            term = term[..MaxTermLength];
        }

        var terms = term.ToLowerInvariant()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct()
            .Take(MaxTerms)
            .ToList();

        var results = new List<(PublishedPosts.Row, int)>();
        if (terms.Count == 0)
        {
            return results;
        }

        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var rank = 0;
            var title = row.title.ToLowerInvariant();
            var description = row.description.ToLowerInvariant();
            var content = row.content.ToLowerInvariant();

            foreach (var t in terms)
            {
                if (t.Length < 4 && rank > 0)
                {
                    continue;
                }

                rank += categories[row.id].Count(c => string.Equals(c.Title, t, StringComparison.OrdinalIgnoreCase)) * 10;
                rank += CountOccurrences(title, t) * 10;
                rank += CountOccurrences(description, t) * 3;
                rank += CountOccurrences(content, t);
            }

            if (rank > 0)
            {
                results.Add((row, rank));
            }
        }

        // Stable tie-break on newest first so equal-ranked results don't shuffle between calls.
        return results
            .OrderByDescending(r => r.Item2)
            .ThenByDescending(r => r.Item1.published_at)
            .ToList();
    }

    // Non-overlapping occurrences - the same count Regex.Matches gave for an escaped literal.
    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        for (var index = haystack.IndexOf(needle, StringComparison.Ordinal);
             index >= 0;
             index = haystack.IndexOf(needle, index + needle.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }
}
