using Dapper.Contrib.Extensions;
using Microsoft.AspNetCore.Http.HttpResults;
using Npgsql;
using ProjectA.Api.Data;

namespace ProjectA.Api.Features.Bookmarks.DeleteBookmark;

public static class DeleteBookmarkEndpoint
{
    public static void MapDeleteBookmark(this RouteGroupBuilder group)
    {
        group.MapDelete("{id}", Handle)
            .WithName("DeleteBookmark")
            .WithSummary("Delete a bookmark")
            .WithDescription(
                "Permanently removes a bookmark by Id. Any category associations for this " +
                "bookmark are removed too, but the categories themselves are never touched.")
            .RequireAuthorization();
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> Handle(
        uint id,
        IDbConnectionFactory connectionFactory,
        CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        bool deleted;
        try
        {
            // A bookmark's Category associations should never block, or be affected by,
            // deleting the bookmark - only this bookmark's bookmark_categories rows are
            // cleared here (via a "replace with nothing"); the categories themselves are
            // never touched.
            await BookmarkCategoryLinks.ReplaceAsync(connection, transaction, (long)id, categoryIds: null, cancellationToken);

            deleted = await connection.DeleteAsync(new BookmarkDto { id = (long)id }, transaction);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.ForeignKeyViolation)
        {
            transaction.Rollback();

            // note_bookmarks and project_bookmarks can still legitimately block deletion -
            // only the category relationship is exempted above.
            return TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Bookmark is in use",
                detail: $"Bookmark {id} is still referenced by other records and cannot be deleted.",
                type: "https://tools.ietf.org/html/rfc7231#section-6.5.8");
        }

        if (!deleted)
        {
            transaction.Rollback();

            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Bookmark not found",
                detail: $"No bookmark exists with id {id}.",
                type: "https://tools.ietf.org/html/rfc7231#section-6.5.4");
        }

        transaction.Commit();
        return TypedResults.NoContent();
    }
}
