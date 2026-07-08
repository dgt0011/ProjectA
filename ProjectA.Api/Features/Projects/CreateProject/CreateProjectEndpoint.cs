using Dapper.Contrib.Extensions;
using Microsoft.AspNetCore.Http.HttpResults;
using ProjectA.Api.Data;

namespace ProjectA.Api.Features.Projects.CreateProject;

public static class CreateProjectEndpoint
{
    public static void MapCreateProject(this RouteGroupBuilder group)
    {
        group.MapPost("", Handle)
            .WithName("CreateProject")
            .WithSummary("Create a project")
            .WithDescription("Creates a new project.");
    }

    private static async Task<Results<CreatedAtRoute<ProjectResponse>, ValidationProblem>> Handle(
        CreateProjectRequest request,
        IDbConnectionFactory connectionFactory,
        CancellationToken cancellationToken = default)
    {
        var errors = Validate(request);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var entity = new ProjectDto
        {
            title = request.Title,
            description = request.Description,
            start_date = request.StartDate!.Value
        };

        using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
        await connection.InsertAsync(entity);

        var response = new ProjectResponse(entity.id, entity.title, entity.description, entity.start_date);

        return TypedResults.CreatedAtRoute(response, "GetProjectById", new { id = response.Id });
    }

    private static Dictionary<string, string[]> Validate(CreateProjectRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            errors[nameof(request.Title)] = ["Title is required."];
        }

        if (request.StartDate is null)
        {
            errors[nameof(request.StartDate)] = ["StartDate is required."];
        }

        return errors;
    }

    // Request body accepted by this endpoint - owned by this slice, not shared.
    public sealed record CreateProjectRequest(string Title, string? Description, DateOnly? StartDate);

    // Shape returned to callers of this endpoint - owned by this slice, not shared.
    public sealed record ProjectResponse(long Id, string Title, string? Description, DateOnly StartDate);
}
