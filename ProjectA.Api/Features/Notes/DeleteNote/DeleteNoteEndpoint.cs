using Dapper.Contrib.Extensions;
using Microsoft.AspNetCore.Http.HttpResults;
using Npgsql;
using ProjectA.Api.Data;

namespace ProjectA.Api.Features.Notes.DeleteNote;

public static class DeleteNoteEndpoint
{
    public static void MapDeleteNote(this RouteGroupBuilder group)
    {
        group.MapDelete("{id}", Handle)
            .WithName("DeleteNote")
            .WithSummary("Delete a note")
            .WithDescription("Permanently removes a note by Id.");
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> Handle(
        uint id,
        IDbConnectionFactory connectionFactory,
        CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);

        bool deleted;
        try
        {
            deleted = await connection.DeleteAsync(new NoteDto { id = (long)id });
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.ForeignKeyViolation)
        {
            // note_categories, note_bookmarks, note_attachments and project_notes reference notes(id).
            return TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Note is in use",
                detail: $"Note {id} is still referenced by other records and cannot be deleted.",
                type: "https://tools.ietf.org/html/rfc7231#section-6.5.8");
        }

        if (!deleted)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Note not found",
                detail: $"No note exists with id {id}.",
                type: "https://tools.ietf.org/html/rfc7231#section-6.5.4");
        }

        return TypedResults.NoContent();
    }
}
