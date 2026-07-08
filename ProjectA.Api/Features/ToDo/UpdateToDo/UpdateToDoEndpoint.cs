using Dapper.Contrib.Extensions;
using Microsoft.AspNetCore.Http.HttpResults;
using ProjectA.Api.Data;
using ProjectA.Api.Features.ToDo;

namespace ProjectA.Api.Features.ToDo.UpdateToDo;

public static class UpdateToDoEndpoint
{
    public static void MapUpdateToDo(this RouteGroupBuilder group)
    {
        group.MapPut("{id}", Handle)
            .WithName("UpdateToDo")
            .WithSummary("Update a ToDo")
            .WithDescription("Replaces an existing ToDo's title, category, description and completion state.");
    }

    private static async Task<Results<Ok<ToDoResponse>, ValidationProblem, ProblemHttpResult>> Handle(
        uint id,
        UpdateToDoRequest request,
        IDbConnectionFactory connectionFactory,
        CancellationToken cancellationToken = default)
    {
        var errors = Validate(request);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var notFound = TypedResults.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "ToDo not found",
            detail: $"No ToDo exists with id {id}.",
            type: "https://tools.ietf.org/html/rfc7231#section-6.5.4");

        using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);

        // Dapper.Contrib's UpdateAsync writes every non-key property back to the row, so the
        // existing entity has to be loaded first - otherwise fields the request doesn't carry
        // (like date_created) would be overwritten with their C# default values.
        var entity = await connection.GetAsync<ToDoEntity>((long)id);
        if (entity is null)
        {
            return notFound;
        }

        entity.title = request.Title;
        entity.category = request.Category;
        entity.description = request.Description;
        entity.actioned = request.Done;
        entity.date_modified = DateTime.UtcNow;

        var updated = await connection.UpdateAsync(entity);
        if (!updated)
        {
            // Row was deleted between the read above and this write.
            return notFound;
        }

        var response = new ToDoResponse(
            entity.id,
            entity.category,
            entity.title,
            entity.description,
            entity.date_created,
            entity.date_modified,
            entity.actioned);

        return TypedResults.Ok(response);
    }

    private static Dictionary<string, string[]> Validate(UpdateToDoRequest request)
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
    public sealed record UpdateToDoRequest(string Title, string Category, string? Description, bool Done);

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
