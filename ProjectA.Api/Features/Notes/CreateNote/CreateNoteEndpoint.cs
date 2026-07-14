using Dapper.Contrib.Extensions;
using Microsoft.AspNetCore.Http.HttpResults;
using Npgsql;
using ProjectA.Api.Data;

namespace ProjectA.Api.Features.Notes.CreateNote;

public static class CreateNoteEndpoint
{
    public static void MapCreateNote(this RouteGroupBuilder group)
    {
        group.MapPost("", Handle)
            .WithName("CreateNote")
            .WithSummary("Create a note")
            .WithDescription(
                "Creates a new note, optionally associating it with existing bookmarks and/or attachments.")
            .RequireAuthorization();
    }

    private static async Task<Results<CreatedAtRoute<NoteResponse>, ValidationProblem>> Handle(
        CreateNoteRequest request,
        IDbConnectionFactory connectionFactory,
        CancellationToken cancellationToken = default)
    {
        var errors = Validate(request);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var entity = new NoteDto
        {
            title = request.Title,
            description = request.Description,
            body = request.Body,
            parent_note_id = request.ParentNoteId,
            date_created = DateTime.UtcNow
        };

        using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        // A brand-new note can't yet be anyone's ancestor (nothing points to it until after
        // this insert), so ParentNoteId only needs the existence check the FK constraint
        // already gives us - no cycle check needed here (unlike UpdateNoteEndpoint).
        try
        {
            await connection.InsertAsync(entity, transaction);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.ForeignKeyViolation)
        {
            transaction.Rollback();

            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.ParentNoteId)] = ["ParentNoteId does not refer to an existing note."]
            });
        }

        List<long> bookmarkIds;
        try
        {
            bookmarkIds = await NoteBookmarkLinks.ReplaceAsync(
                connection, transaction, entity.id, request.BookmarkIds, cancellationToken);
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
            attachmentIds = await NoteAttachmentLinks.ReplaceAsync(
                connection, transaction, entity.id, request.AttachmentIds, cancellationToken);
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

        var response = new NoteResponse(
            entity.id,
            entity.title,
            entity.description,
            entity.body,
            entity.parent_note_id,
            entity.date_created,
            entity.date_modified,
            bookmarkIds,
            attachmentIds);

        return TypedResults.CreatedAtRoute(response, "GetNoteById", new { id = response.Id });
    }

    private static Dictionary<string, string[]> Validate(CreateNoteRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        // Neither column is NOT NULL in the schema, but a note with no title and no body
        // isn't a usable record, so this endpoint requires at least one of them.
        if (string.IsNullOrWhiteSpace(request.Title) && string.IsNullOrWhiteSpace(request.Body))
        {
            const string message = "Either Title or Body is required.";
            errors[nameof(request.Title)] = [message];
            errors[nameof(request.Body)] = [message];
        }

        return errors;
    }

    // Request body accepted by this endpoint - owned by this slice, not shared.
    public sealed record CreateNoteRequest(
        string? Title,
        string? Description,
        string? Body,
        long? ParentNoteId,
        IReadOnlyCollection<long>? BookmarkIds,
        IReadOnlyCollection<long>? AttachmentIds);

    // Shape returned to callers of this endpoint - owned by this slice, not shared.
    public sealed record NoteResponse(
        long Id,
        string? Title,
        string? Description,
        string? Body,
        long? ParentNoteId,
        DateTime DateCreated,
        DateTime? DateModified,
        IReadOnlyCollection<long> BookmarkIds,
        IReadOnlyCollection<long> AttachmentIds);
}
