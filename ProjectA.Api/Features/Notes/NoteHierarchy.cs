using System.Data;
using Dapper;

namespace ProjectA.Api.Features.Notes;

// parent_note_id is a plain self-referencing column (not a join table), so unlike
// NoteBookmarkLinks/NoteAttachmentLinks there's nothing to replace/clear - just this one guard
// against turning the tree into a cycle, which the FK constraint alone can't catch.
internal static class NoteHierarchy
{
    // True if setting noteId's parent to proposedParentId would create a cycle - i.e.
    // proposedParentId is noteId itself, or already a descendant of noteId. Walks the
    // proposed parent's own ancestor chain looking for noteId; finding it there means noteId
    // is an ancestor of (or equal to) proposedParentId, so linking them the other way around
    // would loop back on itself.
    public static async Task<bool> WouldCreateCycleAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        long noteId,
        long proposedParentId,
        CancellationToken cancellationToken)
    {
        if (noteId == proposedParentId)
        {
            return true;
        }

        const string sql = """
            WITH RECURSIVE ancestors AS (
                SELECT id, parent_note_id FROM notes WHERE id = @ProposedParentId
                UNION ALL
                SELECT n.id, n.parent_note_id
                FROM notes n
                JOIN ancestors a ON n.id = a.parent_note_id
            )
            SELECT EXISTS (SELECT 1 FROM ancestors WHERE id = @NoteId);
            """;

        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            sql,
            new { NoteId = noteId, ProposedParentId = proposedParentId },
            transaction,
            cancellationToken: cancellationToken));
    }
}
