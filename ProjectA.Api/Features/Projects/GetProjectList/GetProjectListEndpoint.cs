using Dapper.Contrib.Extensions;
using Microsoft.AspNetCore.Http.HttpResults;
using ProjectA.Api.Data;

namespace ProjectA.Api.Features.Projects.GetProjectList;

public static class GetProjectListEndpoint
{
    public static void MapGetProjectList(this RouteGroupBuilder group)
    {
        group.MapGet("", Handle)
            .WithName("GetProjectList")
            .WithSummary("List projects")
            .WithDescription("Returns all projects.");
    }

    private static async Task<Ok<List<ProjectListItemResponse>>> Handle(
        IDbConnectionFactory connectionFactory,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
            var entities = await connection.GetAllAsync<ProjectDto>();

            var items = entities
                .Select(entity => new ProjectListItemResponse(
                    entity.id,
                    entity.title,
                    entity.description,
                    entity.start_date))
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
    public sealed record ProjectListItemResponse(long Id, string Title, string? Description, DateOnly StartDate);
}
