using System.Data;
using Dapper;

namespace ProjectA.Api.Features.Blog;

// Shared by every slice that reads or writes a post's categories. Like Blogifier, posts are
// tagged by category *title* from the editor - unknown titles are created on the fly, known
// ones (matched case-insensitively) are reused.
internal static class BlogPostCategoryLinks
{
    public sealed record PostCategory(long PostId, long Id, string Title);

    public static async Task<ILookup<long, PostCategory>> GetForPostsAsync(
        IDbConnection connection, IReadOnlyCollection<long> postIds, CancellationToken cancellationToken)
    {
        if (postIds.Count == 0)
        {
            return Array.Empty<PostCategory>().ToLookup(c => c.PostId);
        }

        var rows = await connection.QueryAsync<PostCategory>(new CommandDefinition(
            """
            SELECT pc.post_id AS PostId, c.id AS Id, c.title AS Title
            FROM blog_post_categories pc
            JOIN blog_categories c ON c.id = pc.category_id
            WHERE pc.post_id = ANY(@PostIds)
            ORDER BY c.title;
            """,
            new { PostIds = postIds.ToArray() },
            cancellationToken: cancellationToken));

        return rows.ToLookup(c => c.PostId);
    }

    public static async Task ReplaceAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        long postId,
        IEnumerable<string>? categoryTitles,
        CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM blog_post_categories WHERE post_id = @PostId;",
            new { PostId = postId },
            transaction,
            cancellationToken: cancellationToken));

        foreach (var title in Normalize(categoryTitles))
        {
            var categoryId = await connection.ExecuteScalarAsync<long?>(new CommandDefinition(
                "SELECT id FROM blog_categories WHERE LOWER(title) = LOWER(@Title) ORDER BY id LIMIT 1;",
                new { Title = title },
                transaction,
                cancellationToken: cancellationToken));

            categoryId ??= await connection.ExecuteScalarAsync<long>(new CommandDefinition(
                "INSERT INTO blog_categories (title, date_created) VALUES (@Title, now()) RETURNING id;",
                new { Title = title },
                transaction,
                cancellationToken: cancellationToken));

            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO blog_post_categories (post_id, category_id) VALUES (@PostId, @CategoryId)
                ON CONFLICT DO NOTHING;
                """,
                new { PostId = postId, CategoryId = categoryId },
                transaction,
                cancellationToken: cancellationToken));
        }
    }

    public const int MaxTitleLength = 120;

    public static IEnumerable<string> Normalize(IEnumerable<string>? categoryTitles) =>
        (categoryTitles ?? [])
            .Select(t => t.Trim())
            .Where(t => t.Length > 0)
            .DistinctBy(t => t.ToLowerInvariant());
}
