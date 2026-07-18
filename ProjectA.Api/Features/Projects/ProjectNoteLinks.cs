using System.Data;
using Dapper;

namespace ProjectA.Api.Features.Projects;

// Shared helper for managing the project_notes many-to-many join table - mirrors
// NoteBookmarkLinks exactly, just for project<->note instead of note<->bookmark. A project
// owns these rows, so deleting a project clears them rather than being blocked by them; a note
// referenced by a project blocks deletion of the note instead (the opposite direction).
internal static class ProjectNoteLinks
{
    // Full replace: clears the project's existing note associations and inserts the given
    // set. Returns the distinct, sorted set actually persisted.
    public static async Task<List<long>> ReplaceAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        long projectId,
        IReadOnlyCollection<long>? noteIds,
        CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM project_notes WHERE project_id = @ProjectId;",
            new { ProjectId = projectId },
            transaction,
            cancellationToken: cancellationToken));

        var distinctIds = noteIds?.Distinct().Order().ToList() ?? [];
        if (distinctIds.Count == 0)
        {
            return distinctIds;
        }

        var rows = distinctIds.Select(noteId => new { ProjectId = projectId, NoteId = noteId });

        await connection.ExecuteAsync(new CommandDefinition(
            "INSERT INTO project_notes (project_id, note_id) VALUES (@ProjectId, @NoteId);",
            rows,
            transaction,
            cancellationToken: cancellationToken));

        return distinctIds;
    }

    public static async Task<List<long>> GetNoteIdsAsync(
        IDbConnection connection,
        long projectId,
        CancellationToken cancellationToken,
        IDbTransaction? transaction = null)
    {
        var ids = await connection.QueryAsync<long>(new CommandDefinition(
            "SELECT note_id FROM project_notes WHERE project_id = @ProjectId ORDER BY note_id;",
            new { ProjectId = projectId },
            transaction,
            cancellationToken: cancellationToken));

        return ids.ToList();
    }

    // Bulk variant for GetProjectListEndpoint - one query for every project's associations
    // instead of one query per project.
    public static async Task<ILookup<long, long>> GetNoteIdsForAllProjectsAsync(
        IDbConnection connection,
        CancellationToken cancellationToken)
    {
        var rows = await connection.QueryAsync<ProjectNoteRow>(new CommandDefinition(
            "SELECT project_id, note_id FROM project_notes;",
            cancellationToken: cancellationToken));

        return rows.ToLookup(row => row.project_id, row => row.note_id);
    }

    // ReSharper disable InconsistentNaming
    private sealed record ProjectNoteRow(long project_id, long note_id);
    // ReSharper restore InconsistentNaming
}
