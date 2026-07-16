using Dapper.Contrib.Extensions;
using Microsoft.AspNetCore.Http.HttpResults;
using Npgsql;
using ProjectA.Api.Data;

namespace ProjectA.Api.Features.Projects.CreateProject;

public static class CreateProjectEndpoint
{
    public static void MapCreateProject(this RouteGroupBuilder group)
    {
        group.MapPost("", Handle)
            .WithName("CreateProject")
            .WithSummary("Create a project")
            .WithDescription(
                "Creates a new project, optionally associating it with existing notes, " +
                "bookmarks and/or attachments.")
            .RequireAuthorization();
    }

    private static async Task<Results<CreatedAtRoute<ProjectResponse>, ValidationProblem>> Handle(
        CreateProjectRequest request,
        IDbConnectionFactory connectionFactory,
        CancellationToken cancellationToken = default)
    {
        var errors = Validate(request);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var entity = new ProjectDto
        {
            title = request.Title,
            description = request.Description,
            start_date = request.StartDate!.Value,
            is_private = request.IsPrivate
        };

        using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        await connection.InsertAsync(entity, transaction);

        // Three separate try/catches (same convention as CreateNoteEndpoint's BookmarkIds vs.
        // AttachmentIds) so a bad id in one collection is attributed to that collection
        // specifically, rather than being lumped in with the others.
        List<long> noteIds;
        try
        {
            noteIds = await ProjectNoteLinks.ReplaceAsync(connection, transaction, entity.id, request.NoteIds, cancellationToken);
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
            bookmarkIds = await ProjectBookmarkLinks.ReplaceAsync(connection, transaction, entity.id, request.BookmarkIds, cancellationToken);
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
            attachmentIds = await ProjectAttachmentLinks.ReplaceAsync(connection, transaction, entity.id, request.AttachmentIds, cancellationToken);
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

        var response = new ProjectResponse(
            entity.id, entity.title, entity.description, entity.start_date, entity.is_private, noteIds, bookmarkIds, attachmentIds);

        return TypedResults.CreatedAtRoute(response, "GetProjectById", new { id = response.Id });
    }

    private static Dictionary<string, string[]> Validate(CreateProjectRequest request)
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
    public sealed record CreateProjectRequest(
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
