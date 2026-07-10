using Dapper.Contrib.Extensions;
using Microsoft.AspNetCore.Http.HttpResults;
using ProjectA.Api.Data;

namespace ProjectA.Api.Features.Projects.UpdateProject;

public static class UpdateProjectEndpoint
{
    public static void MapUpdateProject(this RouteGroupBuilder group)
    {
        group.MapPut("{id}", Handle)
            .WithName("UpdateProject")
            .WithSummary("Update a project")
            .WithDescription("Replaces an existing project's title, description and start date.");
    }

    private static async Task<Results<Ok<ProjectResponse>, ValidationProblem, ProblemHttpResult>> Handle(
        uint id,
        UpdateProjectRequest request,
        IDbConnectionFactory connectionFactory,
        CancellationToken cancellationToken = default)
    {
        var errors = Validate(request);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        // A project has no fields the request doesn't already carry, so this can build the
        // entity directly and rely on UpdateAsync's affected-row count for the not-found case
        // (same reasoning as UpdateCategoryEndpoint).
        var entity = new ProjectDto
        {
            id = (long)id,
            title = request.Title,
            description = request.Description,
            start_date = request.StartDate!.Value
        };

        using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
        var updated = await connection.UpdateAsync(entity);

        if (!updated)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Project not found",
                detail: $"No project exists with id {id}.",
                type: "https://tools.ietf.org/html/rfc7231#section-6.5.4");
        }

        return TypedResults.Ok(new ProjectResponse(entity.id, entity.title, entity.description, entity.start_date));
    }

    private static Dictionary<string, string[]> Validate(UpdateProjectRequest request)
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
    public sealed record UpdateProjectRequest(string Title, string? Description, DateOnly? StartDate);

    // Shape returned to callers of this endpoint - owned by this slice, not shared.
    public sealed record ProjectResponse(long Id, string Title, string? Description, DateOnly StartDate);
}
