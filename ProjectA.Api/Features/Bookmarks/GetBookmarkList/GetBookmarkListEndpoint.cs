using Dapper.Contrib.Extensions;
using Microsoft.AspNetCore.Http.HttpResults;
using ProjectA.Api.Data;

namespace ProjectA.Api.Features.Bookmarks.GetBookmarkList;

public static class GetBookmarkListEndpoint
{
    public static void MapGetBookmarkList(this RouteGroupBuilder group)
    {
        group.MapGet("", Handle)
            .WithName("GetBookmarkList")
            .WithSummary("List bookmarks")
            .WithDescription("Returns all bookmarks.");
    }

    private static async Task<Ok<List<BookmarkListItemResponse>>> Handle(
        IDbConnectionFactory connectionFactory,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
            var entities = await connection.GetAllAsync<BookmarkDto>();

            var items = entities
                .Select(entity => new BookmarkListItemResponse(
                    entity.id,
                    entity.url,
                    entity.title,
                    entity.description,
                    entity.rating,
                    entity.date_created,
                    entity.date_modified))
                .ToList();

            return TypedResults.Ok(items);
        }
        catch (Exception)
        {
            // TODO: Some logging is necessary
            return TypedResults.Ok(new List<BookmarkListItemResponse>());
        }
    }

    // Shape returned to callers of this endpoint - owned by this slice, not shared.
    public sealed record BookmarkListItemResponse(
        long Id,
        string Url,
        string? Title,
        string? Description,
        int Rating,
        DateTime DateCreated,
        DateTime? DateModified);
}
