using System.Security.Claims;
using Dapper.Contrib.Extensions;
using Microsoft.AspNetCore.Http.HttpResults;
using ProjectA.Api.Data;

namespace ProjectA.Api.Features.Notes.GetNoteById;

public static class GetNoteByIdEndpoint
{
    public static void MapGetNoteById(this RouteGroupBuilder group)
    {
        group.MapGet("{id}", Handle)
            .WithName("GetNoteById")
            .WithSummary("Get a note by Id")
            .WithDescription(
                "Returns a single note by Id. Private notes (and notes that inherit privacy " +
                "from a private ancestor) return 404 to callers who aren't logged in, exactly " +
                "as if they didn't exist.");
    }

    // This endpoint is deliberately not .RequireAuthorization() - anyone can look up a public
    // note. But app.UseAuthentication() still validates any bearer token that IS presented, so
    // ClaimsPrincipal here reflects "the caller happens to be logged in" without forcing it.
    private static async Task<Results<Ok<NoteResponse>, ProblemHttpResult>> Handle(
        uint id,
        ClaimsPrincipal user,
        IDbConnectionFactory connectionFactory,
        CancellationToken cancellationToken = default)
    {
        var notFound = TypedResults.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Note not found",
            detail: $"No note exists with id {id}.",
            type: "https://tools.ietf.org/html/rfc7231#section-6.5.4");

        try
        {
            using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
            var entity = await connection.GetAsync<NoteDto>((long)id);

            if (entity is not null)
            {
                if (user.Identity?.IsAuthenticated != true)
                {
                    var privateNoteIds = await NotePrivacy.GetEffectivelyPrivateNoteIdsAsync(connection, cancellationToken);
                    if (privateNoteIds.Contains(entity.id))
                    {
                        return notFound;
                    }
                }

                var bookmarkIds = await NoteBookmarkLinks.GetBookmarkIdsAsync(connection, entity.id, cancellationToken);
                var attachmentIds = await NoteAttachmentLinks.GetAttachmentIdsAsync(connection, entity.id, cancellationToken);

                var response = new NoteResponse(
                    entity.id,
                    entity.title,
                    entity.description,
                    entity.body,
                    entity.parent_note_id,
                    entity.is_private,
                    entity.date_created,
                    entity.date_modified,
                    bookmarkIds,
                    attachmentIds);

                return TypedResults.Ok(response);
            }
        }
        catch (Exception)
        {
            // TODO: Some logging is necessary
        }

        return notFound;
    }

    // Shape returned to callers of this endpoint - owned by this slice, not shared.
    public sealed record NoteResponse(
        long Id,
        string? Title,
        string? Description,
        string? Body,
        long? ParentNoteId,
        bool IsPrivate,
        DateTime DateCreated,
        DateTime? DateModified,
        IReadOnlyCollection<long> BookmarkIds,
        IReadOnlyCollection<long> AttachmentIds);
}
