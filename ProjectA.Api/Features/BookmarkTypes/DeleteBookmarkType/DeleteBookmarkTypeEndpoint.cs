using Dapper.Contrib.Extensions;
using Microsoft.AspNetCore.Http.HttpResults;
using Npgsql;
using ProjectA.Api.Data;

namespace ProjectA.Api.Features.BookmarkTypes.DeleteBookmarkType;

public static class DeleteBookmarkTypeEndpoint
{
    public static void MapDeleteBookmarkType(this RouteGroupBuilder group)
    {
        group.MapDelete("{id}", Handle)
            .WithName("DeleteBookmarkType")
            .WithSummary("Delete a bookmark type")
            .WithDescription("Permanently removes a bookmark type by Id.")
            .RequireAuthorization();
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
            deleted = await connection.DeleteAsync(new BookmarkTypeDto { id = (long)id });
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.ForeignKeyViolation)
        {
            // bookmarks.bookmark_type_id references bookmark_types(id).
            return TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Bookmark type is in use",
                detail: $"Bookmark type {id} is still assigned to one or more bookmarks and cannot be deleted.",
                type: "https://tools.ietf.org/html/rfc7231#section-6.5.8");
        }

        if (!deleted)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Bookmark type not found",
                detail: $"No bookmark type exists with id {id}.",
                type: "https://tools.ietf.org/html/rfc7231#section-6.5.4");
        }

        return TypedResults.NoContent();
    }
}
