using Dapper.Contrib.Extensions;
using Microsoft.AspNetCore.Http.HttpResults;
using Npgsql;
using ProjectA.Api.Data;

namespace ProjectA.Api.Features.Projects.UpdateProject;

public static class UpdateProjectEndpoint
{
    public static void MapUpdateProject(this RouteGroupBuilder group)
    {
        group.MapPut("{id}", Handle)
            .WithName("UpdateProject")
            .WithSummary("Update a project")
            .WithDescription(
                "Replaces an existing project's title, description and start date. If " +
                "NoteIds/BookmarkIds/AttachmentIds is supplied it replaces that association " +
                "(pass an empty array to clear it) - omit it entirely to leave it unchanged.")
            .RequireAuthorization();
    }

    private static async Task<Results<Ok<ProjectResponse>, ValidationProblem, ProblemHttpResult>> Handle(
        uint id,
        UpdateProjectRequest request,
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
            title: "Project not found",
            detail: $"No project exists with id {id}.",
            type: "https://tools.ietf.org/html/rfc7231#section-6.5.4");

        // A project has no fields the request doesn't already carry, so this can build the
        // entity directly and rely on UpdateAsync's affected-row count for the not-found case
        // (same reasoning as UpdateCategoryEndpoint).
        var entity = new ProjectDto
        {
            id = (long)id,
            title = request.Title,
            description = request.Description,
            start_date = request.StartDate!.Value,
            is_private = request.IsPrivate
        };

        using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        var updated = await connection.UpdateAsync(entity, transaction);
        if (!updated)
        {
            transaction.Rollback();
            return notFound;
        }

        // Null means "don't touch the association" (same convention as Notes' BookmarkIds/
        // AttachmentIds); an explicit list, even empty, replaces it. Three separate
        // try/catches so a bad id in one collection is attributed to that collection alone.
        List<long> noteIds;
        try
        {
            noteIds = request.NoteIds is not null
                ? await ProjectNoteLinks.ReplaceAsync(connection, transaction, entity.id, request.NoteIds, cancellationToken)
                : await ProjectNoteLinks.GetNoteIdsAsync(connection, entity.id, cancellationToken, transaction);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.ForeignKeyViolation)
        {
            transaction.Rollback();

            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.NoteIds)] = ["One or more NoteIds do not refer to an existing note."]
            });
        }

        List<long> bookmarkIds;
        try
        {
            bookmarkIds = request.BookmarkIds is not null
                ? await ProjectBookmarkLinks.ReplaceAsync(connection, transaction, entity.id, request.BookmarkIds, cancellationToken)
                : await ProjectBookmarkLinks.GetBookmarkIdsAsync(connection, entity.id, cancellationToken, transaction);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.ForeignKeyViolation)
        {
            transaction.Rollback();

            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.BookmarkIds)] = ["One or more BookmarkIds do not refer to an existing bookmark."]
            });
        }

        List<long> attachmentIds;
        try
        {
            attachmentIds = request.AttachmentIds is not null
                ? await ProjectAttachmentLinks.ReplaceAsync(connection, transaction, entity.id, request.AttachmentIds, cancellationToken)
                : await ProjectAttachmentLinks.GetAttachmentIdsAsync(connection, entity.id, cancellationToken, transaction);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.ForeignKeyViolation)
        {
            transaction.Rollback();

            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.AttachmentIds)] = ["One or more AttachmentIds do not refer to an existing attachment."]
            });
        }

        transaction.Commit();

        return TypedResults.Ok(new ProjectResponse(
            entity.id, entity.title, entity.description, entity.start_date, entity.is_private, noteIds, bookmarkIds, attachmentIds));
    }

    private static Dictionary<string, string[]> Validate(UpdateProjectRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            errors[nameof(request.Title)] = ["Title is required."];
        }

        if (request.StartDate is null)
        {
            errors[nameof(request.StartDate)] = ["StartDate is required."];
        }

        return errors;
    }

    // Request body accepted by this endpoint - owned by this slice, not shared.
    public sealed record UpdateProjectRequest(
        string Title,
        string? Description,
        DateTime? StartDate,
        bool IsPrivate,
        IReadOnlyCollection<long>? NoteIds,
        IReadOnlyCollection<long>? BookmarkIds,
        IReadOnlyCollection<long>? AttachmentIds);

    // Shape returned to callers of this endpoint - owned by this slice, not shared.
    public sealed record ProjectResponse(
        long Id,
        string Title,
        string? Description,
        DateTime StartDate,
        bool IsPrivate,
        IReadOnlyCollection<long> NoteIds,
        IReadOnlyCollection<long> BookmarkIds,
        IReadOnlyCollection<long> AttachmentIds);
}
