using Dapper.Contrib.Extensions;
using Microsoft.AspNetCore.Http.HttpResults;
using ProjectA.Api.Data;

namespace ProjectA.Api.Features.ToDo.GetToDoById;

public static class GetToDoByIdEndpoint
{
    public static void MapGetToDoById(this RouteGroupBuilder group)
    {
        group.MapGet("{id}", Handle)
            .WithName("GetToDoById")
            .WithSummary("Get a ToDo by Id")
            .WithDescription("Returns a single ToDo by Id, including completed items.");
    }

    private static async Task<Results<Ok<ToDoResponse>, ProblemHttpResult>> Handle(
        uint id,
        IDbConnectionFactory connectionFactory,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
            var entity = await connection.GetAsync<ToDoDto>((long)id);

            if (entity is not null)
            {
                var response = new ToDoResponse(
                    entity.id,
                    entity.category_id,
                    entity.title,
                    entity.description,
                    entity.date_created,
                    entity.date_modified,
                    entity.actioned);

                return TypedResults.Ok(response);
            }
        }
        catch (Exception)
        {
            // TODO: Some logging is necessary
        }

        return TypedResults.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "ToDo not found",
            detail: $"No ToDo exists with id {id}.",
            type: "https://tools.ietf.org/html/rfc7231#section-6.5.4");
    }

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
