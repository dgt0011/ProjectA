using Dapper.Contrib.Extensions;
using Microsoft.AspNetCore.Http.HttpResults;
using Npgsql;
using ProjectA.Api.Data;

namespace ProjectA.Api.Features.Projects.DeleteProject;

public static class DeleteProjectEndpoint
{
    public static void MapDeleteProject(this RouteGroupBuilder group)
    {
        group.MapDelete("{id}", Handle)
            .WithName("DeleteProject")
            .WithSummary("Delete a project")
            .WithDescription(
                "Permanently removes a project by Id. Any note, bookmark and attachment " +
                "associations for this project are removed too, but the notes, bookmarks and " +
                "attachments themselves are never touched.")
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
            // A project's Note, Bookmark and Attachment associations should never block, or
            // be affected by, deleting the project - the project is the "owning" side of all
            // three relationships (same direction as a note owning note_bookmarks/
            // note_attachments), so only this project's own join rows are cleared here; the
            // notes/bookmarks/attachments themselves are never touched.
            await ProjectNoteLinks.ReplaceAsync(connection, transaction, (long)id, noteIds: null, cancellationToken);
            await ProjectBookmarkLinks.ReplaceAsync(connection, transaction, (long)id, bookmarkIds: null, cancellationToken);
            await ProjectAttachmentLinks.ReplaceAsync(connection, transaction, (long)id, attachmentIds: null, cancellationToken);

            deleted = await connection.DeleteAsync(new ProjectDto { id = (long)id }, transaction);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.ForeignKeyViolation)
        {
            transaction.Rollback();

            // todos.project_id is the only relationship that can still legitimately block
            // deletion here - a ToDo referencing this project, the opposite direction from the
            // three relationships exempted above.
            return TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Project is in use",
                detail: $"Project {id} is still referenced by other records and cannot be deleted.",
                type: "https://tools.ietf.org/html/rfc7231#section-6.5.8");
        }

        if (!deleted)
        {
            transaction.Rollback();

            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Project not found",
                detail: $"No project exists with id {id}.",
                type: "https://tools.ietf.org/html/rfc7231#section-6.5.4");
        }

        transaction.Commit();
        return TypedResults.NoContent();
    }
}
