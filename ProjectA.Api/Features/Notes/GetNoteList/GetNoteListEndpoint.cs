using Dapper.Contrib.Extensions;
using Microsoft.AspNetCore.Http.HttpResults;
using ProjectA.Api.Data;

namespace ProjectA.Api.Features.Notes.GetNoteList;

public static class GetNoteListEndpoint
{
    public static void MapGetNoteList(this RouteGroupBuilder group)
    {
        group.MapGet("", Handle)
            .WithName("GetNoteList")
            .WithSummary("List notes")
            .WithDescription("Returns all notes.");
    }

    private static async Task<Ok<List<NoteListItemResponse>>> Handle(
        IDbConnectionFactory connectionFactory,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
            var entities = await connection.GetAllAsync<NoteDto>();

            var bookmarkIdsByNote = await NoteBookmarkLinks.GetBookmarkIdsForAllNotesAsync(connection, cancellationToken);
            var attachmentIdsByNote = await NoteAttachmentLinks.GetAttachmentIdsForAllNotesAsync(connection, cancellationToken);

            var items = entities
                .Select(entity => new NoteListItemResponse(
                    entity.id,
                    entity.title,
                    entity.description,
                    entity.body,
                    entity.date_created,
                    entity.date_modified,
                    bookmarkIdsByNote[entity.id].ToList(),
                    attachmentIdsByNote[entity.id].ToList()))
                .ToList();

            return TypedResults.Ok(items);
        }
        catch (Exception)
        {
            // TODO: Some logging is necessary
            return TypedResults.Ok(new List<NoteListItemResponse>());
        }
    }

    // Shape returned to callers of this endpoint - owned by this slice, not shared.
    public sealed record NoteListItemResponse(
        long Id,
        string? Title,
        string? Description,
        string? Body,
        DateTime DateCreated,
        DateTime? DateModified,
        IReadOnlyCollection<long> BookmarkIds,
        IReadOnlyCollection<long> AttachmentIds);
}
