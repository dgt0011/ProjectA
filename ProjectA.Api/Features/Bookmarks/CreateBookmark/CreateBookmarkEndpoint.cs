using Dapper.Contrib.Extensions;
using Microsoft.AspNetCore.Http.HttpResults;
using Npgsql;
using ProjectA.Api.Data;

namespace ProjectA.Api.Features.Bookmarks.CreateBookmark;

public static class CreateBookmarkEndpoint
{
    public static void MapCreateBookmark(this RouteGroupBuilder group)
    {
        group.MapPost("", Handle)
            .WithName("CreateBookmark")
            .WithSummary("Create a bookmark")
            .WithDescription("Creates a new bookmark, optionally associating it with one or more categories.")
            .RequireAuthorization();
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
            bookmark_type_id = request.BookmarkTypeId,
            date_created = DateTime.UtcNow
        };

        using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        // Two separate try/catches (same convention as CreateNoteEndpoint's BookmarkIds vs.
        // AttachmentIds): the insert itself is the only place a bad BookmarkTypeId can fail, so
        // it's caught and attributed on its own rather than lumped in with the CategoryIds
        // catch below.
        try
        {
            await connection.InsertAsync(entity, transaction);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.ForeignKeyViolation)
        {
            transaction.Rollback();

            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.BookmarkTypeId)] = ["BookmarkTypeId does not refer to an existing bookmark type."]
            });
        }

        List<long> categoryIds;
        try
        {
            // Same transaction as the insert above: if any CategoryId doesn't exist, the
            // whole create is rolled back rather than leaving an uncategorized bookmark behind.
            categoryIds = await BookmarkCategoryLinks.ReplaceAsync(
                connection, transaction, entity.id, request.CategoryIds, cancellationToken);

            transaction.Commit();
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.ForeignKeyViolation)
        {
            transaction.Rollback();

            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.CategoryIds)] = ["One or more CategoryIds do not refer to an existing category."]
            });
        }

        var response = new BookmarkResponse(
            entity.id,
            entity.url,
            entity.title,
            entity.description,
            entity.rating,
            entity.bookmark_type_id,
            entity.date_created,
            entity.date_modified,
            categoryIds);

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
    public sealed record CreateBookmarkRequest(
        string Url,
        string? Title,
        string? Description,
        int? Rating,
        long? BookmarkTypeId,
        IReadOnlyCollection<long>? CategoryIds);

    // Shape returned to callers of this endpoint - owned by this slice, not shared.
    public sealed record BookmarkResponse(
        long Id,
        string Url,
        string? Title,
        string? Description,
        int Rating,
        long? BookmarkTypeId,
        DateTime DateCreated,
        DateTime? DateModified,
        IReadOnlyCollection<long> CategoryIds);
}
