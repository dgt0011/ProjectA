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
            .WithDescription("Permanently removes a bookmark by Id.");
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> Handle(
        uint id,
        IDbConnectionFactory connectionFactory,
        CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);

        bool deleted;
        try
        {
            deleted = await connection.DeleteAsync(new BookmarkDto { id = (long)id });
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.ForeignKeyViolation)
        {
            // bookmark_categories, note_bookmarks and project_bookmarks reference bookmarks(id).
            return TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Bookmark is in use",
                detail: $"Bookmark {id} is still referenced by other records and cannot be deleted.",
                type: "https://tools.ietf.org/html/rfc7231#section-6.5.8");
        }

        if (!deleted)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Bookmark not found",
                detail: $"No bookmark exists with id {id}.",
                type: "https://tools.ietf.org/html/rfc7231#section-6.5.4");
        }

        return TypedResults.NoContent();
    }
}
