using Dapper;
using Dapper.Contrib.Extensions;
using Microsoft.AspNetCore.Http.HttpResults;
using Npgsql;
using ProjectA.Api.Data;
using ProjectA.Api.Features.Notes.UpdateNote;

namespace ProjectA.Api.Features.Notes.DeleteNote;

public static class DeleteNoteEndpoint
{
    public static void MapDeleteNote(this RouteGroupBuilder group)
    {
        group.MapDelete("{id}", Handle)
            .WithName("DeleteNote")
            .WithSummary("Delete a note")
            .WithDescription(
                "Permanently removes a note by Id. Any category, bookmark and attachment " +
                "associations for this note are removed too, but the categories, bookmarks " +
                "and attachments themselves are never touched.")
            .RequireAuthorization();
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> Handle(
        uint id,
        IDbConnectionFactory connectionFactory,
        CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        bool deleted;
        try
        {
            // A note's Category, Bookmark and Attachment associations should never block, or
            // be affected by, deleting the note - the note is the "owning" side of all three
            // relationships (same direction as a bookmark owning bookmark_categories), so only
            // this note's own join rows are cleared here; the categories/bookmarks/attachments
            // themselves are never touched.
            await connection.ExecuteAsync(new CommandDefinition(
                "DELETE FROM note_categories WHERE note_id = @NoteId;",
                new { NoteId = (long)id },
                transaction,
                cancellationToken: cancellationToken));

            await NoteCategoryLinks.ReplaceAsync(connection, transaction, (long)id, categoryIds: null, cancellationToken);
            await NoteBookmarkLinks.ReplaceAsync(connection, transaction, (long)id, bookmarkIds: null, cancellationToken);
            await NoteAttachmentLinks.ReplaceAsync(connection, transaction, (long)id, attachmentIds: null, cancellationToken);

            deleted = await connection.DeleteAsync(new NoteDto { id = (long)id }, transaction);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.ForeignKeyViolation)
        {
            transaction.Rollback();

            // project_notes (a Project referencing this note) and any child note still
            // pointing at this one via parent_note_id are the relationships that can still
            // legitimately block deletion here - the opposite direction from the three
            // relationships exempted above. A note with children has to be re-parented or
            // have them deleted first, same as any other "referenced" entity in this API.
            return TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Note is in use",
                detail: $"Note {id} is still referenced by other records and cannot be deleted.",
                type: "https://tools.ietf.org/html/rfc7231#section-6.5.8");
        }

        if (!deleted)
        {
            transaction.Rollback();

            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Note not found",
                detail: $"No note exists with id {id}.",
                type: "https://tools.ietf.org/html/rfc7231#section-6.5.4");
        }

        transaction.Commit();
        return TypedResults.NoContent();
    }
}
