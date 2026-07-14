using System.Data;
using Dapper;

namespace ProjectA.Api.Features.Projects;

// Shared helper for managing the project_attachments many-to-many join table - mirrors
// NoteAttachmentLinks exactly, just for project<->attachment. A project owns these rows, so
// deleting a project clears them rather than being blocked by them; an attachment referenced
// by a project blocks deletion of the attachment instead (the opposite direction).
internal static class ProjectAttachmentLinks
{
    public static async Task<List<long>> ReplaceAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        long projectId,
        IReadOnlyCollection<long>? attachmentIds,
        CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM project_attachments WHERE project_id = @ProjectId;",
            new { ProjectId = projectId },
            transaction,
            cancellationToken: cancellationToken));

        var distinctIds = attachmentIds?.Distinct().Order().ToList() ?? [];
        if (distinctIds.Count == 0)
        {
            return distinctIds;
        }

        var rows = distinctIds.Select(attachmentId => new { ProjectId = projectId, AttachmentId = attachmentId });

        await connection.ExecuteAsync(new CommandDefinition(
            "INSERT INTO project_attachments (project_id, attachment_id) VALUES (@ProjectId, @AttachmentId);",
            rows,
            transaction,
            cancellationToken: cancellationToken));

        return distinctIds;
    }

    public static async Task<List<long>> GetAttachmentIdsAsync(
        IDbConnection connection,
        long projectId,
        CancellationToken cancellationToken,
        IDbTransaction? transaction = null)
    {
        var ids = await connection.QueryAsync<long>(new CommandDefinition(
            "SELECT attachment_id FROM project_attachments WHERE project_id = @ProjectId ORDER BY attachment_id;",
            new { ProjectId = projectId },
            transaction,
            cancellationToken: cancellationToken));

        return ids.ToList();
    }

    // Bulk variant for GetProjectListEndpoint - one query for every project's associations
    // instead of one query per project.
    public static async Task<ILookup<long, long>> GetAttachmentIdsForAllProjectsAsync(
        IDbConnection connection,
        CancellationToken cancellationToken)
    {
        var rows = await connection.QueryAsync<ProjectAttachmentRow>(new CommandDefinition(
            "SELECT project_id, attachment_id FROM project_attachments;",
            cancellationToken: cancellationToken));

        return rows.ToLookup(row => row.project_id, row => row.attachment_id);
    }

    // ReSharper disable InconsistentNaming
    private sealed record ProjectAttachmentRow(long project_id, long attachment_id);
    // ReSharper restore InconsistentNaming
}
