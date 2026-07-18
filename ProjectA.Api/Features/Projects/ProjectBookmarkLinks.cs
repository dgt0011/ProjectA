using System.Data;
using Dapper;

namespace ProjectA.Api.Features.Projects;

// Shared helper for managing the project_bookmarks many-to-many join table - mirrors
// NoteBookmarkLinks exactly, just for project<->bookmark. A project owns these rows, so
// deleting a project clears them rather than being blocked by them; a bookmark referenced by a
// project blocks deletion of the bookmark instead (the opposite direction - see
// DeleteBookmarkEndpoint).
internal static class ProjectBookmarkLinks
{
    public static async Task<List<long>> ReplaceAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        long projectId,
        IReadOnlyCollection<long>? bookmarkIds,
        CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM project_bookmarks WHERE project_id = @ProjectId;",
            new { ProjectId = projectId },
            transaction,
            cancellationToken: cancellationToken));

        var distinctIds = bookmarkIds?.Distinct().Order().ToList() ?? [];
        if (distinctIds.Count == 0)
        {
            return distinctIds;
        }

        var rows = distinctIds.Select(bookmarkId => new { ProjectId = projectId, BookmarkId = bookmarkId });

        await connection.ExecuteAsync(new CommandDefinition(
            "INSERT INTO project_bookmarks (project_id, bookmark_id) VALUES (@ProjectId, @BookmarkId);",
            rows,
            transaction,
            cancellationToken: cancellationToken));

        return distinctIds;
    }

    public static async Task<List<long>> GetBookmarkIdsAsync(
        IDbConnection connection,
        long projectId,
        CancellationToken cancellationToken,
        IDbTransaction? transaction = null)
    {
        var ids = await connection.QueryAsync<long>(new CommandDefinition(
            "SELECT bookmark_id FROM project_bookmarks WHERE project_id = @ProjectId ORDER BY bookmark_id;",
            new { ProjectId = projectId },
            transaction,
            cancellationToken: cancellationToken));

        return ids.ToList();
    }

    // Bulk variant for GetProjectListEndpoint - one query for every project's associations
    // instead of one query per project.
    public static async Task<ILookup<long, long>> GetBookmarkIdsForAllProjectsAsync(
        IDbConnection connection,
        CancellationToken cancellationToken)
    {
        var rows = await connection.QueryAsync<ProjectBookmarkRow>(new CommandDefinition(
            "SELECT project_id, bookmark_id FROM project_bookmarks;",
            cancellationToken: cancellationToken));

        return rows.ToLookup(row => row.project_id, row => row.bookmark_id);
    }

    // ReSharper disable InconsistentNaming
    private sealed record ProjectBookmarkRow(long project_id, long bookmark_id);
    // ReSharper restore InconsistentNaming
}
