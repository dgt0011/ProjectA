using Microsoft.AspNetCore.Http.HttpResults;

namespace ProjectA.Api.ToDo;

public static class ToDoEndpoints
{
    public static void RegisterTodoEndpoints(this WebApplication app)
    {
        app.MapGet("/todo", GetToDoList);
        app.MapGet("/todo/{id}", GetToDoById);
    }
        
    static async Task<Results<Ok<List<ToDo>>, NotFound>> GetToDoList(IToDoService service)
    {
        var list = await service.GetList();
        return TypedResults.Ok(list);
    }

    static async Task<Results<Ok<ToDo>, NotFound>> GetToDoById(uint id, IToDoService service) =>
        await service.GetById(id)
            is { } todo
            ? TypedResults.Ok(todo)
            : TypedResults.NotFound();
}