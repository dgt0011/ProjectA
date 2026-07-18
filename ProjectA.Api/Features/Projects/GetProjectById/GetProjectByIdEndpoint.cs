using System.Security.Claims;
using Dapper.Contrib.Extensions;
using Microsoft.AspNetCore.Http.HttpResults;
using ProjectA.Api.Data;
using ProjectA.Api.Features.Notes;

namespace ProjectA.Api.Features.Projects.GetProjectById;

public static class GetProjectByIdEndpoint
{
    public static void MapGetProjectById(this RouteGroupBuilder group)
    {
        group.MapGet("{id}", Handle)
            .WithName("GetProjectById")
            .WithSummary("Get a project by Id")
            .WithDescription(
                "Returns a single project by Id. A private project returns 404 to callers who " +
                "aren't logged in, exactly as if it didn't exist. NoteIds also excludes any " +
                "associated note that's private (or inherits privacy from a private ancestor) " +
                "when the caller isn't logged in - same rule GetNoteList/GetNoteById apply to " +
                "notes directly.");
    }

    // Not .RequireAuthorization() - anyone can look up a public project. See
    // GetNoteByIdEndpoint for why ClaimsPrincipal can still be trusted here to distinguish
    // logged-in callers without forcing authentication on this endpoint.
    private static async Task<Results<Ok<ProjectResponse>, ProblemHttpResult>> Handle(
        uint id,
        ClaimsPrincipal user,
        IDbConnectionFactory connectionFactory,
        CancellationToken cancellationToken = default)
    {
        var notFound = TypedResults.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Project not found",
            detail: $"No project exists with id {id}.",
            type: "https://tools.ietf.org/html/rfc7231#section-6.5.4");

        try
        {
            using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
            var entity = await connection.GetAsync<ProjectDto>((long)id);

            if (entity is not null)
            {
                var isAuthenticated = user.Identity?.IsAuthenticated == true;

                // A private project is treated as if it doesn't exist to a caller who isn't
                // logged in - same 404, no distinguishing response shape/status, so its
                // existence can't be inferred by an anonymous caller.
                if (entity.is_private && !isAuthenticated)
                {
                    return notFound;
                }

                var noteIds = await ProjectNoteLinks.GetNoteIdsAsync(connection, entity.id, cancellationToken);
                var bookmarkIds = await ProjectBookmarkLinks.GetBookmarkIdsAsync(connection, entity.id, cancellationToken);
                var attachmentIds = await ProjectAttachmentLinks.GetAttachmentIdsAsync(connection, entity.id, cancellationToken);

                if (!isAuthenticated)
                {
                    var privateNoteIds = await NotePrivacy.GetEffectivelyPrivateNoteIdsAsync(connection, cancellationToken);
                    noteIds = noteIds.Where(noteId => !privateNoteIds.Contains(noteId)).ToList();
                }

                var response = new ProjectResponse(
                    entity.id, entity.title, entity.description, entity.start_date, entity.is_private,
                    noteIds, bookmarkIds, attachmentIds);
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
    public sealed record ProjectResponse(
        long Id,
        string Title,
        string? Description,
        DateTime StartDate,
        bool IsPrivate,
        IReadOnlyCollection<long> NoteIds,
        IReadOnlyCollection<long> BookmarkIds,
        IReadOnlyCollection<long> AttachmentIds);
}
