using Dapper.Contrib.Extensions;
using Microsoft.AspNetCore.Http.HttpResults;
using Npgsql;
using ProjectA.Api.Data;

namespace ProjectA.Api.Features.ToDo.CreateToDo;

public static class CreateToDoEndpoint
{
    public static void MapCreateToDo(this RouteGroupBuilder group)
    {
        group.MapPost("", Handle)
            .WithName("CreateToDo")
            .WithSummary("Create a ToDo")
            .WithDescription("Creates a new outstanding ToDo item, associated with exactly one category.")
            .RequireAuthorization();
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

        var entity = new ToDoDto
        {
            title = request.Title,
            actioned = false,
            category_id = request.CategoryId,
            description = request.Description,
            date_created = DateTime.UtcNow
        };

        using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);

        try
        {
            await connection.InsertAsync(entity);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.ForeignKeyViolation)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.CategoryId)] = ["CategoryId does not refer to an existing category."]
            });
        }

        var response = new ToDoResponse(
            entity.id,
            entity.category_id,
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

        if (request.CategoryId is null)
        {
            errors[nameof(request.CategoryId)] = ["CategoryId is required."];
        }

        return errors;
    }

    // Request body accepted by this endpoint - owned by this slice, not shared.
    public sealed record CreateToDoRequest(string Title, long? CategoryId, string? Description);

    // Shape returned to callers of this endpoint - owned by this slice, not shared.
    public sealed record ToDoResponse(
        long Id,
        long? CategoryId,
        string Title,
        string? Description,
        DateTime? DateCreated,
        DateTime? DateModified,
        bool Done);
}
