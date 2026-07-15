using System.Security.Claims;
using Dapper.Contrib.Extensions;
using Microsoft.AspNetCore.Http.HttpResults;
using ProjectA.Api.Data;
using ProjectA.Api.Features.Notes;

namespace ProjectA.Api.Features.Projects.GetProjectList;

public static class GetProjectListEndpoint
{
    public static void MapGetProjectList(this RouteGroupBuilder group)
    {
        group.MapGet("", Handle)
            .WithName("GetProjectList")
            .WithSummary("List projects")
            .WithDescription(
                "Returns all projects. Each project's NoteIds excludes any associated note " +
                "that's private (or inherits privacy from a private ancestor) when the caller " +
                "isn't logged in - same rule GetNoteList/GetNoteById apply to notes directly.");
    }

    // Not .RequireAuthorization() - anyone can list public projects. See GetNoteListEndpoint
    // for why ClaimsPrincipal can still be trusted here without forcing authentication.
    private static async Task<Ok<List<ProjectListItemResponse>>> Handle(
        ClaimsPrincipal user,
        IDbConnectionFactory connectionFactory,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
            var entities = await connection.GetAllAsync<ProjectDto>();

            var noteIdsByProject = await ProjectNoteLinks.GetNoteIdsForAllProjectsAsync(connection, cancellationToken);
            var bookmarkIdsByProject = await ProjectBookmarkLinks.GetBookmarkIdsForAllProjectsAsync(connection, cancellationToken);
            var attachmentIdsByProject = await ProjectAttachmentLinks.GetAttachmentIdsForAllProjectsAsync(connection, cancellationToken);

            var privateNoteIds = user.Identity?.IsAuthenticated == true
                ? new HashSet<long>()
                : await NotePrivacy.GetEffectivelyPrivateNoteIdsAsync(connection, cancellationToken);

            var items = entities
                .Select(entity => new ProjectListItemResponse(
                    entity.id,
                    entity.title,
                    entity.description,
                    entity.start_date,
                    noteIdsByProject[entity.id].Where(noteId => !privateNoteIds.Contains(noteId)).ToList(),
                    bookmarkIdsByProject[entity.id].ToList(),
                    attachmentIdsByProject[entity.id].ToList()))
                .ToList();

            return TypedResults.Ok(items);
        }
        catch (Exception)
        {
            // TODO: Some logging is necessary
            return TypedResults.Ok(new List<ProjectListItemResponse>());
        }
    }

    // Shape returned to callers of this endpoint - owned by this slice, not shared.
    public sealed record ProjectListItemResponse(
        long Id,
        string Title,
        string? Description,
        DateTime StartDate,
        IReadOnlyCollection<long> NoteIds,
        IReadOnlyCollection<long> BookmarkIds,
        IReadOnlyCollection<long> AttachmentIds);
}
