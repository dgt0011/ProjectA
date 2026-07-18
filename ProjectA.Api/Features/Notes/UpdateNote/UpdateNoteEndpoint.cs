using Dapper.Contrib.Extensions;
using Microsoft.AspNetCore.Http.HttpResults;
using Npgsql;
using ProjectA.Api.Data;

namespace ProjectA.Api.Features.Notes.UpdateNote;

public static class UpdateNoteEndpoint
{
    public static void MapUpdateNote(this RouteGroupBuilder group)
    {
        group.MapPut("{id}", Handle)
            .WithName("UpdateNote")
            .WithSummary("Update a note")
            .WithDescription(
                "Replaces an existing note's title, description and body. If BookmarkIds/" +
                "AttachmentIds is supplied it replaces that association (pass an empty array " +
                "to clear it) - omit it entirely to leave it unchanged.")
            .RequireAuthorization();
    }

    private static async Task<Results<Ok<NoteResponse>, ValidationProblem, ProblemHttpResult>> Handle(
        uint id,
        UpdateNoteRequest request,
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
            title: "Note not found",
            detail: $"No note exists with id {id}.",
            type: "https://tools.ietf.org/html/rfc7231#section-6.5.4");

        using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        var entity = await connection.GetAsync<NoteDto>((long)id, transaction);
        if (entity is null)
        {
            transaction.Rollback();
            return notFound;
        }

        // ParentNoteId is checked for cycles before it's ever handed to Postgres - the FK
        // constraint alone guarantees the parent exists, but it has no way to know that
        // "existing" parent isn't actually one of this note's own descendants, which would
        // turn the tree into a loop.
        if (request.ParentNoteId is { } parentNoteId &&
            await NoteHierarchy.WouldCreateCycleAsync(connection, transaction, entity.id, parentNoteId, cancellationToken))
        {
            transaction.Rollback();

            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.ParentNoteId)] = ["A note cannot be its own parent, or a descendant of itself."]
            });
        }

        entity.title = request.Title;
        entity.description = request.Description;
        entity.body = request.Body;
        entity.parent_note_id = request.ParentNoteId;
        entity.is_private = request.IsPrivate;
        entity.date_modified = DateTime.UtcNow;

        bool updated;
        try
        {
            updated = await connection.UpdateAsync(entity, transaction);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.ForeignKeyViolation)
        {
            transaction.Rollback();

            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.ParentNoteId)] = ["ParentNoteId does not refer to an existing note."]
            });
        }

        if (!updated)
        {
            transaction.Rollback();
            return notFound;
        }

        // Null means "don't touch the association" (same convention as Bookmarks'
        // CategoryIds); an explicit list, even empty, replaces it.
        List<long> bookmarkIds;
        try
        {
            bookmarkIds = request.BookmarkIds is not null
                ? await NoteBookmarkLinks.ReplaceAsync(connection, transaction, entity.id, request.BookmarkIds, cancellationToken)
                : await NoteBookmarkLinks.GetBookmarkIdsAsync(connection, entity.id, cancellationToken, transaction);
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
                ? await NoteAttachmentLinks.ReplaceAsync(connection, transaction, entity.id, request.AttachmentIds, cancellationToken)
                : await NoteAttachmentLinks.GetAttachmentIdsAsync(connection, entity.id, cancellationToken, transaction);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.ForeignKeyViolation)
        {
            transaction.Rollback();

            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.AttachmentIds)] = ["One or more AttachmentIds do not refer to an existing attachment."]
            });
        }
        
        List<long> categoryIds;
        try
        {
            // Null CategoryIds means "don't touch the associations" (same null-means-unchanged
            // convention as Rating above); an explicit list, even empty, replaces them.
            categoryIds = request.CategoryIds is not null
                ? await NoteCategoryLinks.ReplaceAsync(connection, transaction, entity.id, request.CategoryIds, cancellationToken)
                : await NoteCategoryLinks.GetCategoryIdsAsync(connection, entity.id, cancellationToken, transaction);

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
        
        var response = new NoteResponse(
            entity.id,
            entity.title,
            entity.description,
            entity.body,
            entity.parent_note_id,
            entity.is_private,
            entity.date_created,
            entity.date_modified,
            bookmarkIds,
            attachmentIds,
            categoryIds);

        return TypedResults.Ok(response);
    }

    private static Dictionary<string, string[]> Validate(UpdateNoteRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Title) && string.IsNullOrWhiteSpace(request.Body))
        {
            const string message = "Either Title or Body is required.";
            errors[nameof(request.Title)] = [message];
            errors[nameof(request.Body)] = [message];
        }

        return errors;
    }

    // Request body accepted by this endpoint - owned by this slice, not shared. ParentNoteId
    // and IsPrivate are plain scalars (not collections) so, like Title/Description/Body,
    // they're always overwritten outright rather than following the
    // CategoryIds/BookmarkIds/AttachmentIds "null means unchanged" convention - the Web form's
    // dropdown/checkbox always reflect the current selection, including "no parent"/"not
    // private".
    public sealed record UpdateNoteRequest(
        string? Title,
        string? Description,
        string? Body,
        long? ParentNoteId,
        bool IsPrivate,
        IReadOnlyCollection<long>? BookmarkIds,
        IReadOnlyCollection<long>? AttachmentIds,
        IReadOnlyCollection<long>? CategoryIds);

    // Shape returned to callers of this endpoint - owned by this slice, not shared.
    public sealed record NoteResponse(
        long Id,
        string? Title,
        string? Description,
        string? Body,
        long? ParentNoteId,
        bool IsPrivate,
        DateTime DateCreated,
        DateTime? DateModified,
        IReadOnlyCollection<long> BookmarkIds,
        IReadOnlyCollection<long> AttachmentIds,
        IReadOnlyCollection<long> CategoryIds);
}
