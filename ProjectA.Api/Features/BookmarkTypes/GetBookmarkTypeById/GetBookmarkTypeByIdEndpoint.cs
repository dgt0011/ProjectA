using Dapper.Contrib.Extensions;
using Microsoft.AspNetCore.Http.HttpResults;
using ProjectA.Api.Data;

namespace ProjectA.Api.Features.BookmarkTypes.GetBookmarkTypeById;

public static class GetBookmarkTypeByIdEndpoint
{
    public static void MapGetBookmarkTypeById(this RouteGroupBuilder group)
    {
        group.MapGet("{id}", Handle)
            .WithName("GetBookmarkTypeById")
            .WithSummary("Get a bookmark type by Id")
            .WithDescription("Returns a single bookmark type by Id.");
    }

    private static async Task<Results<Ok<BookmarkTypeResponse>, ProblemHttpResult>> Handle(
        uint id,
        IDbConnectionFactory connectionFactory,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
            var entity = await connection.GetAsync<BookmarkTypeDto>((long)id);

            if (entity is not null)
            {
                return TypedResults.Ok(new BookmarkTypeResponse(entity.id, entity.title, entity.color, entity.icon is not null));
            }
        }
        catch (Exception)
        {
            // TODO: Some logging is necessary
        }

        return TypedResults.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Bookmark type not found",
            detail: $"No bookmark type exists with id {id}.",
            type: "https://tools.ietf.org/html/rfc7231#section-6.5.4");
    }

    // Shape returned to callers of this endpoint - owned by this slice, not shared.
    public sealed record BookmarkTypeResponse(long Id, string Title, string? Color, bool HasIcon);
}
