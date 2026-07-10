using Dapper.Contrib.Extensions;
using Microsoft.AspNetCore.Http.HttpResults;
using ProjectA.Api.Data;

namespace ProjectA.Api.Features.Projects.GetProjectById;

public static class GetProjectByIdEndpoint
{
    public static void MapGetProjectById(this RouteGroupBuilder group)
    {
        group.MapGet("{id}", Handle)
            .WithName("GetProjectById")
            .WithSummary("Get a project by Id")
            .WithDescription("Returns a single project by Id.");
    }

    private static async Task<Results<Ok<ProjectResponse>, ProblemHttpResult>> Handle(
        uint id,
        IDbConnectionFactory connectionFactory,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
            var entity = await connection.GetAsync<ProjectDto>((long)id);

            if (entity is not null)
            {
                var response = new ProjectResponse(entity.id, entity.title, entity.description, entity.start_date);
                return TypedResults.Ok(response);
            }
        }
        catch (Exception)
        {
            // TODO: Some logging is necessary
        }

        return TypedResults.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Project not found",
            detail: $"No project exists with id {id}.",
            type: "https://tools.ietf.org/html/rfc7231#section-6.5.4");
    }

    // Shape returned to callers of this endpoint - owned by this slice, not shared.
    public sealed record ProjectResponse(long Id, string Title, string? Description, DateTime StartDate);
}
