using System.Data;
using Dapper;

namespace ProjectA.Api.Features.Notes;

// A note is "effectively private" if it is itself flagged is_private, or if any ancestor
// along its parent_note_id chain is - privacy is never opted back out of by a child, it only
// ever cascades downward (see the feature description: "If a Private Note has Child Notes
// that are not marked as Private, then they inherit the Private setting of the Parent Note").
// Effective privacy is deliberately computed here at read time rather than stored on each
// descendant row - that way re-parenting a note or toggling a parent's flag doesn't require
// cascading updates across a whole subtree, it just changes what this query returns.
internal static class NotePrivacy
{
    // Every note id that is effectively private: every note with is_private = true, plus the
    // full set of their descendants (transitively), regardless of each descendant's own flag.
    public static async Task<HashSet<long>> GetEffectivelyPrivateNoteIdsAsync(
        IDbConnection connection,
        CancellationToken cancellationToken,
        IDbTransaction? transaction = null)
    {
        const string sql = """
            WITH RECURSIVE private_notes AS (
                SELECT id FROM notes WHERE is_private = true
                UNION
                SELECT n.id
                FROM notes n
                JOIN private_notes p ON n.parent_note_id = p.id
            )
            SELECT id FROM private_notes;
            """;

        var ids = await connection.QueryAsync<long>(new CommandDefinition(
            sql, transaction: transaction, cancellationToken: cancellationToken));

        return ids.ToHashSet();
    }
}
