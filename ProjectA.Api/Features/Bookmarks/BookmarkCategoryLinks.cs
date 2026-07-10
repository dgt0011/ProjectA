using System.Data;
using Dapper;

namespace ProjectA.Api.Features.Bookmarks;

// Shared helper for managing the bookmark_categories many-to-many join table. Dapper.Contrib
// only handles single-table CRUD, so relationship management goes through hand-written SQL -
// kept here rather than duplicated per slice because every slice that touches it has to agree
// on the same join table shape.
internal static class BookmarkCategoryLinks
{
    // Full replace: clears the bookmark's existing category associations and inserts the
    // given set. Returns the distinct, sorted set actually persisted.
    public static async Task<List<long>> ReplaceAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        long bookmarkId,
        IReadOnlyCollection<long>? categoryIds,
        CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM bookmark_categories WHERE bookmark_id = @BookmarkId;",
            new { BookmarkId = bookmarkId },
            transaction,
            cancellationToken: cancellationToken));

        var distinctIds = categoryIds?.Distinct().Order().ToList() ?? [];
        if (distinctIds.Count == 0)
        {
            return distinctIds;
        }

        var rows = distinctIds.Select(categoryId => new { BookmarkId = bookmarkId, CategoryId = categoryId });

        await connection.ExecuteAsync(new CommandDefinition(
            "INSERT INTO bookmark_categories (bookmark_id, category_id) VALUES (@BookmarkId, @CategoryId);",
            rows,
            transaction,
            cancellationToken: cancellationToken));

        return distinctIds;
    }

    public static async Task<List<long>> GetCategoryIdsAsync(
        IDbConnection connection,
        long bookmarkId,
        CancellationToken cancellationToken,
        IDbTransaction? transaction = null)
    {
        var ids = await connection.QueryAsync<long>(new CommandDefinition(
            "SELECT category_id FROM bookmark_categories WHERE bookmark_id = @BookmarkId ORDER BY category_id;",
            new { BookmarkId = bookmarkId },
            transaction,
            cancellationToken: cancellationToken));

        return ids.ToList();
    }
}
