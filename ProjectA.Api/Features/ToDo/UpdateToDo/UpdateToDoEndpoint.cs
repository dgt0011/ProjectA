using Dapper.Contrib.Extensions;
using Microsoft.AspNetCore.Http.HttpResults;
using Npgsql;
using ProjectA.Api.Data;

namespace ProjectA.Api.Features.ToDo.UpdateToDo;

public static class UpdateToDoEndpoint
{
    public static void MapUpdateToDo(this RouteGroupBuilder group)
    {
        group.MapPut("{id}", Handle)
            .WithName("UpdateToDo")
            .WithSummary("Update a ToDo")
            .WithDescription("Replaces an existing ToDo's title, category, description and completion state.")
            .RequireAuthorization();
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
        var entity = await connection.GetAsync<ToDoDto>((long)id);
        if (entity is null)
        {
            return notFound;
        }

        entity.title = request.Title;
        entity.category_id = request.CategoryId;
        entity.project_id = request.ProjectId;
        entity.description = request.Description;
        entity.actioned = request.Done;
        entity.date_modified = DateTime.UtcNow;

        bool updated;
        try
        {
            updated = await connection.UpdateAsync(entity);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.ForeignKeyViolation)
        {
            return TypedResults.ValidationProblem(BuildForeignKeyViolationError(ex, request));
        }

        if (!updated)
        {
            // Row was deleted between the read above and this write.
            return notFound;
        }

        var response = new ToDoResponse(
            entity.id,
            entity.category_id,
            entity.project_id,
            entity.title,
            entity.description,
            entity.completion_notes,
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

        // CategoryId is optional - a ToDo with none is grouped as "Uncategorized" wherever
        // ToDo lists are displayed, rather than being rejected here.
        return errors;
    }

    // Same reasoning as CreateToDoEndpoint's equivalent helper - the constraint name is what
    // tells us whether CategoryId or ProjectId actually failed to resolve.
    private static Dictionary<string, string[]> BuildForeignKeyViolationError(PostgresException ex, UpdateToDoRequest request) =>
        ex.ConstraintName?.Contains("project_id") == true
            ? new Dictionary<string, string[]>
            {
                [nameof(request.ProjectId)] = ["ProjectId does not refer to an existing project."]
            }
            : new Dictionary<string, string[]>
            {
                [nameof(request.CategoryId)] = ["CategoryId does not refer to an existing category."]
            };

    // Request body accepted by this endpoint - owned by this slice, not shared. ProjectId
    // defaults to null so existing positional-argument call sites keep compiling unchanged.
    public sealed record UpdateToDoRequest(string Title, long? CategoryId, string? Description, bool Done, long? ProjectId = null);

    // Shape returned to callers of this endpoint - owned by this slice, not shared.
    public sealed record ToDoResponse(
        long Id,
        long? CategoryId,
        long? ProjectId,
        string Title,
        string? Description,
        string? CompletionNotes,
        DateTime? DateCreated,
        DateTime? DateModified,
        bool Done);
}
