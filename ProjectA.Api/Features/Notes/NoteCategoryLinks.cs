using System.Data;
using Dapper;

namespace ProjectA.Api.Features.Notes;

internal static class NoteCategoryLinks
{
    public static async Task<List<long>> ReplaceAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        long noteId,
        IReadOnlyCollection<long>? categoryIds,
        CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM note_categories WHERE note_id = @NoteId;",
            new { NoteId = noteId },
            transaction,
            cancellationToken: cancellationToken));

        var distinctIds = categoryIds?.Distinct().Order().ToList() ?? [];
        if (distinctIds.Count == 0)
        {
            return distinctIds;
        }

        var rows = distinctIds.Select(categoryId => new { NoteId = noteId, categoryId = categoryId });

        await connection.ExecuteAsync(new CommandDefinition(
            "INSERT INTO note_categories (note_id, category_id) VALUES (@NoteId, @CategoryId);",
            rows,
            transaction,
            cancellationToken: cancellationToken));

        return distinctIds;
    }
    
    public static async Task<List<long>> GetCategoryIdsAsync(
        IDbConnection connection,
        long noteId,
        CancellationToken cancellationToken,
        IDbTransaction? transaction = null)
    {
        var ids = await connection.QueryAsync<long>(new CommandDefinition(
            "SELECT category_id FROM note_categories WHERE note_id = @NoteId ORDER BY category_id;",
            new { NoteId = noteId },
            transaction,
            cancellationToken: cancellationToken));

        return ids.ToList();
    }
    
    public static async Task<ILookup<long, long>> GetCategoryIdsForAllNotesAsync(
        IDbConnection connection,
        CancellationToken cancellationToken)
    {
        var rows = await connection.QueryAsync<NoteCategoryRow>(new CommandDefinition(
            "SELECT note_id, category_id FROM note_categories;",
            cancellationToken: cancellationToken));

        return rows.ToLookup(row => row.note_id, row => row.category_id);
    }
    
    // ReSharper disable InconsistentNaming
    private sealed record NoteCategoryRow(long note_id, long category_id);
    // ReSharper restore InconsistentNaming
}