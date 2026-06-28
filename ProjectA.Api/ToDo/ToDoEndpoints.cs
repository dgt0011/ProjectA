using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace ProjectA.Api.ToDo;

public static class ToDoEndpoints
{
    public static void RegisterTodoEndpoints(this WebApplication app)
    {
        var todo = app.MapGroup("/api/todo")
            .WithTags("ToDo");

        todo.MapGet("", GetToDoList)
            .WithName("GetToDoList")
            .WithSummary("List ToDo items")
            .WithDescription(
                "Returns outstanding ToDo items. Pass includeDone=true to include completed items.");

        todo.MapGet("{id}", GetToDoById)
            .WithName("GetToDoById")
            .WithSummary("Get a ToDo by Id")
            .WithDescription("Returns a single ToDo by Id, including completed items.");
    }

    static async Task<Ok<List<ToDoItem>>> GetToDoList(
        IToDoService service,
        [FromQuery]bool includeDone = false,
        CancellationToken cancellationToken = default)
    {
        var list = await service.GetList(includeDone, cancellationToken);
        return TypedResults.Ok(list);
    }

    static async Task<Results<Ok<ToDoItem>, ProblemHttpResult>> GetToDoById(
        uint id,
        IToDoService service,
        CancellationToken cancellationToken = default)
    {
        var todo = await service.GetById(id, cancellationToken);
        if (todo is not null)
        {
            return TypedResults.Ok(todo);
        }

        return TypedResults.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "ToDo not found",
            detail: $"No ToDo exists with id {id}.",
            type: "https://tools.ietf.org/html/rfc7231#section-6.5.4");
    }
}
