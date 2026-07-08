using Dapper.Contrib.Extensions;
using Microsoft.AspNetCore.Http.HttpResults;
using ProjectA.Api.Data;

namespace ProjectA.Api.Features.ToDo.CreateToDo;

public static class CreateToDoEndpoint
{
    public static void MapCreateToDo(this RouteGroupBuilder group)
    {
        group.MapPost("", Handle)
            .WithName("CreateToDo")
            .WithSummary("Create a ToDo")
            .WithDescription("Creates a new outstanding ToDo item.");
    }

    private static async Task<Results<CreatedAtRoute<ToDoResponse>, ValidationProblem>> Handle(
        CreateToDoRequest request,
        IDbConnectionFactory connectionFactory,
        CancellationToken cancellationToken = default)
    {
        var errors = Validate(request);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var entity = new ToDoEntity
        {
            title = request.Title,
            actioned = false,
            category = request.Category,
            description = request.Description,
            date_created = DateTime.UtcNow
        };

        using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
        await connection.InsertAsync(entity);

        var response = new ToDoResponse(
            entity.id,
            entity.category,
            entity.title,
            entity.description,
            entity.date_created,
            entity.date_modified,
            entity.actioned);

        return TypedResults.CreatedAtRoute(response, "GetToDoById", new { id = response.Id });
    }

    private static Dictionary<string, string[]> Validate(CreateToDoRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            errors[nameof(request.Title)] = ["Title is required."];
        }

        if (string.IsNullOrWhiteSpace(request.Category))
        {
            errors[nameof(request.Category)] = ["Category is required."];
        }

        return errors;
    }

    // Request body accepted by this endpoint - owned by this slice, not shared.
    public sealed record CreateToDoRequest(string Title, string Category, string? Description);

    // Shape returned to callers of this endpoint - owned by this slice, not shared.
    public sealed record ToDoResponse(
        long Id,
        string Category,
        string Title,
        string? Description,
        DateTime? DateCreated,
        DateTime? DateModified,
        bool Done);
}
