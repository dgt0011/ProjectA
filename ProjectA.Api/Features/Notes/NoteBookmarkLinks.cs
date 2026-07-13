using System.Data;
using Dapper;

namespace ProjectA.Api.Features.Notes;

// Shared helper for managing the note_bookmarks many-to-many join table - mirrors
// BookmarkCategoryLinks exactly, just for note<->bookmark instead of bookmark<->category.
// A note owns these rows (same relationship shape as a bookmark owning bookmark_categories),
// so deleting a note clears them rather than being blocked by them.
internal static class NoteBookmarkLinks
{
    // Full replace: clears the note's existing bookmark associations and inserts the given
    // set. Returns the distinct, sorted set actually persisted.
    public static async Task<List<long>> ReplaceAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        long noteId,
        IReadOnlyCollection<long>? bookmarkIds,
        CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM note_bookmarks WHERE note_id = @NoteId;",
            new { NoteId = noteId },
            transaction,
            cancellationToken: cancellationToken));

        var distinctIds = bookmarkIds?.Distinct().Order().ToList() ?? [];
        if (distinctIds.Count == 0)
        {
            return distinctIds;
        }

        var rows = distinctIds.Select(bookmarkId => new { NoteId = noteId, BookmarkId = bookmarkId });

        await connection.ExecuteAsync(new CommandDefinition(
            "INSERT INTO note_bookmarks (note_id, bookmark_id) VALUES (@NoteId, @BookmarkId);",
            rows,
            transaction,
            cancellationToken: cancellationToken));

        return distinctIds;
    }

    public static async Task<List<long>> GetBookmarkIdsAsync(
        IDbConnection connection,
        long noteId,
        CancellationToken cancellationToken,
        IDbTransaction? transaction = null)
    {
        var ids = await connection.QueryAsync<long>(new CommandDefinition(
            "SELECT bookmark_id FROM note_bookmarks WHERE note_id = @NoteId ORDER BY bookmark_id;",
            new { NoteId = noteId },
            transaction,
            cancellationToken: cancellationToken));

        return ids.ToList();
    }

    // Bulk variant for GetNoteListEndpoint - one query for every note's associations instead
    // of one query per note.
    public static async Task<ILookup<long, long>> GetBookmarkIdsForAllNotesAsync(
        IDbConnection connection,
        CancellationToken cancellationToken)
    {
        var rows = await connection.QueryAsync<NoteBookmarkRow>(new CommandDefinition(
            "SELECT note_id, bookmark_id FROM note_bookmarks;",
            cancellationToken: cancellationToken));

        return rows.ToLookup(row => row.note_id, row => row.bookmark_id);
    }

    // ReSharper disable InconsistentNaming
    private sealed record NoteBookmarkRow(long note_id, long bookmark_id);
    // ReSharper restore InconsistentNaming
}
