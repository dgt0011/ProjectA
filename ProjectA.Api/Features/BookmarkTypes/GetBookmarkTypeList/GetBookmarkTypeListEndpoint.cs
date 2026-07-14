using Dapper.Contrib.Extensions;
using Microsoft.AspNetCore.Http.HttpResults;
using ProjectA.Api.Data;

namespace ProjectA.Api.Features.BookmarkTypes.GetBookmarkTypeList;

public static class GetBookmarkTypeListEndpoint
{
    public static void MapGetBookmarkTypeList(this RouteGroupBuilder group)
    {
        group.MapGet("", Handle)
            .WithName("GetBookmarkTypeList")
            .WithSummary("List bookmark types")
            .WithDescription("Returns all bookmark types.");
    }

    private static async Task<Ok<List<BookmarkTypeListItemResponse>>> Handle(
        IDbConnectionFactory connectionFactory,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
            var entities = await connection.GetAllAsync<BookmarkTypeDto>();

            var items = entities
                .Select(entity => new BookmarkTypeListItemResponse(entity.id, entity.title, entity.color, entity.icon is not null))
                .ToList();

            return TypedResults.Ok(items);
        }
        catch (Exception)
        {
            // TODO: Some logging is necessary
            return TypedResults.Ok(new List<BookmarkTypeListItemResponse>());
        }
    }

    // Shape returned to callers of this endpoint - owned by this slice, not shared.
    public sealed record BookmarkTypeListItemResponse(long Id, string Title, string? Color, bool HasIcon);
}
