using Dapper.Contrib.Extensions;
using Microsoft.AspNetCore.Http.HttpResults;
using ProjectA.Api.Data;

namespace ProjectA.Api.Features.ToDo.DeleteToDo;

public static class DeleteToDoEndpoint
{
    public static void MapDeleteToDo(this RouteGroupBuilder group)
    {
        group.MapDelete("{id}", Handle)
            .WithName("DeleteToDo")
            .WithSummary("Delete a ToDo")
            .WithDescription("Permanently removes a ToDo by Id.");
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> Handle(
        uint id,
        IDbConnectionFactory connectionFactory,
        CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
        var deleted = await connection.DeleteAsync(new ToDoDto { id = (long)id });

        if (!deleted)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "ToDo not found",
                detail: $"No ToDo exists with id {id}.",
                type: "https://tools.ietf.org/html/rfc7231#section-6.5.4");
        }

        return TypedResults.NoContent();
    }
}
