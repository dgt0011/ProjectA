using System.Security.Claims;
using Dapper.Contrib.Extensions;
using Microsoft.AspNetCore.Http.HttpResults;
using ProjectA.Api.Data;
using ProjectA.Api.Features.Notes.UpdateNote;

namespace ProjectA.Api.Features.Notes.GetNoteList;

public static class GetNoteListEndpoint
{
    public static void MapGetNoteList(this RouteGroupBuilder group)
    {
        group.MapGet("", Handle)
            .WithName("GetNoteList")
            .WithSummary("List notes")
            .WithDescription(
                "Returns all notes visible to the caller. Callers who aren't logged in never " +
                "see private notes (or notes that inherit privacy from a private ancestor).");
    }

    // Not .RequireAuthorization() - anonymous callers can list public notes, they just get a
    // filtered view. See GetNoteByIdEndpoint for why ClaimsPrincipal can still be trusted here.
    private static async Task<Ok<List<NoteListItemResponse>>> Handle(
        ClaimsPrincipal user,
        IDbConnectionFactory connectionFactory,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
            var entities = await connection.GetAllAsync<NoteDto>();

            if (user.Identity?.IsAuthenticated != true)
            {
                var privateNoteIds = await NotePrivacy.GetEffectivelyPrivateNoteIdsAsync(connection, cancellationToken);
                entities = entities.Where(entity => !privateNoteIds.Contains(entity.id)).ToList();
            }

            var bookmarkIdsByNote = await NoteBookmarkLinks.GetBookmarkIdsForAllNotesAsync(connection, cancellationToken);
            var attachmentIdsByNote = await NoteAttachmentLinks.GetAttachmentIdsForAllNotesAsync(connection, cancellationToken);
            var categoryIdsByNote = await NoteCategoryLinks.GetCategoryIdsForAllNotesAsync(connection, cancellationToken);
            
            var items = entities
                .Select(entity => new NoteListItemResponse(
                    entity.id,
                    entity.title,
                    entity.description,
                    entity.body,
                    entity.parent_note_id,
                    entity.is_private,
                    entity.date_created,
                    entity.date_modified,
                    bookmarkIdsByNote[entity.id].ToList(),
                    attachmentIdsByNote[entity.id].ToList(),
                    categoryIdsByNote[entity.id].ToList()))
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
        long? ParentNoteId,
        bool IsPrivate,
        DateTime DateCreated,
        DateTime? DateModified,
        IReadOnlyCollection<long> BookmarkIds,
        IReadOnlyCollection<long> AttachmentIds,
        IReadOnlyCollection<long> CategoryIds);
}
