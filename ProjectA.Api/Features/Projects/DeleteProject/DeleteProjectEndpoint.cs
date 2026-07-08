using Dapper.Contrib.Extensions;
using Microsoft.AspNetCore.Http.HttpResults;
using Npgsql;
using ProjectA.Api.Data;

namespace ProjectA.Api.Features.Projects.DeleteProject;

public static class DeleteProjectEndpoint
{
    public static void MapDeleteProject(this RouteGroupBuilder group)
    {
        group.MapDelete("{id}", Handle)
            .WithName("DeleteProject")
            .WithSummary("Delete a project")
            .WithDescription("Permanently removes a project by Id.");
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> Handle(
        uint id,
        IDbConnectionFactory connectionFactory,
        CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);

        bool deleted;
        try
        {
            deleted = await connection.DeleteAsync(new ProjectDto { id = (long)id });
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.ForeignKeyViolation)
        {
            // project_bookmarks, project_attachments, project_notes and todos.project_id
            // reference projects(id).
            return TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Project is in use",
                detail: $"Project {id} is still referenced by other records and cannot be deleted.",
                type: "https://tools.ietf.org/html/rfc7231#section-6.5.8");
        }

        if (!deleted)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Project not found",
                detail: $"No project exists with id {id}.",
                type: "https://tools.ietf.org/html/rfc7231#section-6.5.4");
        }

        return TypedResults.NoContent();
    }
}
