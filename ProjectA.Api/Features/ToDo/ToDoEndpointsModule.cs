using ProjectA.Api.Features.ToDo.CompleteToDo;
using ProjectA.Api.Features.ToDo.CreateToDo;
using ProjectA.Api.Features.ToDo.DeleteToDo;
using ProjectA.Api.Features.ToDo.GetToDoById;
using ProjectA.Api.Features.ToDo.GetToDoList;
using ProjectA.Api.Features.ToDo.UpdateToDo;

namespace ProjectA.Api.Features.ToDo;

public static class ToDoEndpointsModule
{
    public static void MapToDoEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/todo")
            .WithTags("ToDo");

        group.MapGetToDoList();
        group.MapGetToDoById();
        group.MapCreateToDo();
        group.MapUpdateToDo();
        group.MapCompleteToDo();
        group.MapDeleteToDo();
    }
}
