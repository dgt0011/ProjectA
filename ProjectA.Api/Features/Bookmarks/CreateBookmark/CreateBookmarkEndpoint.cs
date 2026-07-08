using Dapper.Contrib.Extensions;
using Microsoft.AspNetCore.Http.HttpResults;
using ProjectA.Api.Data;

namespace ProjectA.Api.Features.Bookmarks.CreateBookmark;

public static class CreateBookmarkEndpoint
{
    public static void MapCreateBookmark(this RouteGroupBuilder group)
    {
        group.MapPost("", Handle)
            .WithName("CreateBookmark")
            .WithSummary("Create a bookmark")
            .WithDescription("Creates a new bookmark.");
    }

    private static async Task<Results<CreatedAtRoute<BookmarkResponse>, ValidationProblem>> Handle(
        CreateBookmarkRequest request,
        IDbConnectionFactory connectionFactory,
        CancellationToken cancellationToken = default)
    {
        var errors = Validate(request);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var entity = new BookmarkDto
        {
            url = request.Url,
            title = request.Title,
            description = request.Description,
            rating = (short)(request.Rating ?? 1),
            date_created = DateTime.UtcNow
        };

        using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
        await connection.InsertAsync(entity);

        var response = new BookmarkResponse(
            entity.id,
            entity.url,
            entity.title,
            entity.description,
            entity.rating,
            entity.date_created,
            entity.date_modified);

        return TypedResults.CreatedAtRoute(response, "GetBookmarkById", new { id = response.Id });
    }

    private static Dictionary<string, string[]> Validate(CreateBookmarkRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Url))
        {
            errors[nameof(request.Url)] = ["Url is required."];
        }

        if (request.Rating is < 1 or > 10)
        {
            errors[nameof(request.Rating)] = ["Rating must be between 1 and 10."];
        }

        return errors;
    }

    // Request body accepted by this endpoint - owned by this slice, not shared.
    public sealed record CreateBookmarkRequest(string Url, string? Title, string? Description, int? Rating);

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
