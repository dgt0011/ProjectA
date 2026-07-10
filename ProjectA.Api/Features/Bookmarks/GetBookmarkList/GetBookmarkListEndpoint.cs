using Dapper;
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
            .WithDescription("Returns all bookmarks, including each one's associated category Ids.");
    }

    private static async Task<Ok<List<BookmarkListItemResponse>>> Handle(
        IDbConnectionFactory connectionFactory,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
            var entities = await connection.GetAllAsync<BookmarkDto>();

            // One bulk query for every bookmark_categories row rather than one lookup per
            // bookmark (which is what BookmarkCategoryLinks.GetCategoryIdsAsync would mean
            // here) - avoids an N+1 as the bookmark list grows.
            var links = await connection.QueryAsync<BookmarkCategoryLink>(
                "SELECT bookmark_id, category_id FROM bookmark_categories;");

            var categoryIdsByBookmark = links
                .GroupBy(link => link.bookmark_id)
                .ToDictionary(
                    group => group.Key,
                    group => (IReadOnlyCollection<long>)group.Select(link => link.category_id).Order().ToList());

            var items = entities
                .Select(entity => new BookmarkListItemResponse(
                    entity.id,
                    entity.url,
                    entity.title,
                    entity.description,
                    entity.rating,
                    entity.date_created,
                    entity.date_modified,
                    categoryIdsByBookmark.GetValueOrDefault(entity.id, Array.Empty<long>())))
                .ToList();

            return TypedResults.Ok(items);
        }
        catch (Exception)
        {
            // TODO: Some logging is necessary
            return TypedResults.Ok(new List<BookmarkListItemResponse>());
        }
    }

    // Row shape for the bulk bookmark_categories query above - private to this slice.
    // ReSharper disable once NotAccessedPositionalProperty.Local
    private sealed record BookmarkCategoryLink(long bookmark_id, long category_id);

    // Shape returned to callers of this endpoint - owned by this slice, not shared.
    public sealed record BookmarkListItemResponse(
        long Id,
        string Url,
        string? Title,
        string? Description,
        int Rating,
        DateTime DateCreated,
        DateTime? DateModified,
        IReadOnlyCollection<long> CategoryIds);
}
