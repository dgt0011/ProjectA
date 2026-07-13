using System.Data;
using Dapper;

namespace ProjectA.Api.Features.Notes;

// Shared helper for managing the note_attachments many-to-many join table - mirrors
// BookmarkCategoryLinks exactly, just for note<->attachment instead of bookmark<->category.
// A note owns these rows (same relationship shape as a bookmark owning bookmark_categories),
// so deleting a note clears them rather than being blocked by them.
internal static class NoteAttachmentLinks
{
    // Full replace: clears the note's existing attachment associations and inserts the given
    // set. Returns the distinct, sorted set actually persisted.
    public static async Task<List<long>> ReplaceAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        long noteId,
        IReadOnlyCollection<long>? attachmentIds,
        CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM note_attachments WHERE note_id = @NoteId;",
            new { NoteId = noteId },
            transaction,
            cancellationToken: cancellationToken));

        var distinctIds = attachmentIds?.Distinct().Order().ToList() ?? [];
        if (distinctIds.Count == 0)
        {
            return distinctIds;
        }

        var rows = distinctIds.Select(attachmentId => new { NoteId = noteId, AttachmentId = attachmentId });

        await connection.ExecuteAsync(new CommandDefinition(
            "INSERT INTO note_attachments (note_id, attachment_id) VALUES (@NoteId, @AttachmentId);",
            rows,
            transaction,
            cancellationToken: cancellationToken));

        return distinctIds;
    }

    public static async Task<List<long>> GetAttachmentIdsAsync(
        IDbConnection connection,
        long noteId,
        CancellationToken cancellationToken,
        IDbTransaction? transaction = null)
    {
        var ids = await connection.QueryAsync<long>(new CommandDefinition(
            "SELECT attachment_id FROM note_attachments WHERE note_id = @NoteId ORDER BY attachment_id;",
            new { NoteId = noteId },
            transaction,
            cancellationToken: cancellationToken));

        return ids.ToList();
    }

    // Bulk variant for GetNoteListEndpoint - one query for every note's associations instead
    // of one query per note.
    public static async Task<ILookup<long, long>> GetAttachmentIdsForAllNotesAsync(
        IDbConnection connection,
        CancellationToken cancellationToken)
    {
        var rows = await connection.QueryAsync<NoteAttachmentRow>(new CommandDefinition(
            "SELECT note_id, attachment_id FROM note_attachments;",
            cancellationToken: cancellationToken));

        return rows.ToLookup(row => row.note_id, row => row.attachment_id);
    }

    // ReSharper disable InconsistentNaming
    private sealed record NoteAttachmentRow(long note_id, long attachment_id);
    // ReSharper restore InconsistentNaming
}
