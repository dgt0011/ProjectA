using Dapper.Contrib.Extensions;
using Microsoft.AspNetCore.Http.HttpResults;
using ProjectA.Api.Data;

namespace ProjectA.Api.Features.Bookmarks.GetBookmarkById;

public static class GetBookmarkByIdEndpoint
{
    public static void MapGetBookmarkById(this RouteGroupBuilder group)
    {
        group.MapGet("{id}", Handle)
            .WithName("GetBookmarkById")
            .WithSummary("Get a bookmark by Id")
            .WithDescription("Returns a single bookmark by Id.");
    }

    private static async Task<Results<Ok<BookmarkResponse>, ProblemHttpResult>> Handle(
        uint id,
        IDbConnectionFactory connectionFactory,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
            var entity = await connection.GetAsync<BookmarkDto>((long)id);

            if (entity is not null)
            {
                var response = new BookmarkResponse(
                    entity.id,
                    entity.url,
                    entity.title,
                    entity.description,
                    entity.rating,
                    entity.date_created,
                    entity.date_modified);

                return TypedResults.Ok(response);
            }
        }
        catch (Exception)
        {
            // TODO: Some logging is necessary
        }

        return TypedResults.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Bookmark not found",
            detail: $"No bookmark exists with id {id}.",
            type: "https://tools.ietf.org/html/rfc7231#section-6.5.4");
    }

    // Shape returned to callers of this endpoint - owned by this slice, not shared.
    public sealed record BookmarkResponse(
        long Id,
        string Url,
        string? Title,
        string? Description,
        int Rating,
        DateTime DateCreated,
        DateTime? DateModified);
}
