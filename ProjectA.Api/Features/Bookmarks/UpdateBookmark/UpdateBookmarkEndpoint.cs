using Dapper.Contrib.Extensions;
using Microsoft.AspNetCore.Http.HttpResults;
using Npgsql;
using ProjectA.Api.Data;

namespace ProjectA.Api.Features.Bookmarks.UpdateBookmark;

public static class UpdateBookmarkEndpoint
{
    public static void MapUpdateBookmark(this RouteGroupBuilder group)
    {
        group.MapPut("{id}", Handle)
            .WithName("UpdateBookmark")
            .WithSummary("Update a bookmark")
            .WithDescription(
                "Replaces an existing bookmark's url, title, description and rating. " +
                "If CategoryIds is supplied it replaces the bookmark's category associations " +
                "(pass an empty array to clear them)
            .RequireAuthorization(); omit CategoryIds entirely to leave them unchanged.");
    }

    private static async Task<Results<Ok<BookmarkResponse>, ValidationProblem, ProblemHttpResult>> Handle(
        uint id,
        UpdateBookmarkRequest request,
        IDbConnectionFactory connectionFactory,
        CancellationToken cancellationToken = default)
    {
        var errors = Validate(request);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var notFound = TypedResults.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Bookmark not found",
            detail: $"No bookmark exists with id {id}.",
            type: "https://tools.ietf.org/html/rfc7231#section-6.5.4");

        using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        // date_created has to be preserved, so the existing row is loaded first rather than
        // building the entity from the request alone (same reasoning as UpdateToDoEndpoint).
        var entity = await connection.GetAsync<BookmarkDto>((long)id, transaction);
        if (entity is null)
        {
            transaction.Rollback();
            return notFound;
        }

        entity.url = request.Url;
        entity.title = request.Title;
        entity.description = request.Description;
        entity.rating = (short)(request.Rating ?? entity.rating);
        entity.date_modified = DateTime.UtcNow;

        List<long> categoryIds;
        try
        {
            var updated = await connection.UpdateAsync(entity, transaction);
            if (!updated)
            {
                transaction.Rollback();
                return notFound;
            }

            // Null CategoryIds means "don't touch the associations" (same null-means-unchanged
            // convention as Rating above); an explicit list, even empty, replaces them.
            categoryIds = request.CategoryIds is not null
                ? await BookmarkCategoryLinks.ReplaceAsync(connection, transaction, entity.id, request.CategoryIds, cancellationToken)
                : await BookmarkCategoryLinks.GetCategoryIdsAsync(connection, entity.id, cancellationToken, transaction);

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
            entity.date_created,
            entity.date_modified,
            categoryIds);

        return TypedResults.Ok(response);
    }

    private static Dictionary<string, string[]> Validate(UpdateBookmarkRequest request)
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
    public sealed record UpdateBookmarkRequest(
        string Url,
        string? Title,
        string? Description,
        int? Rating,
        IReadOnlyCollection<long>? CategoryIds);

    // Shape returned to callers of this endpoint - owned by this slice, not shared.
    public sealed record BookmarkResponse(
        long Id,
        string Url,
        string? Title,
        string? Description,
        int Rating,
        DateTime DateCreated,
        DateTime? DateModified,
        IReadOnlyCollection<long> CategoryIds);
}
