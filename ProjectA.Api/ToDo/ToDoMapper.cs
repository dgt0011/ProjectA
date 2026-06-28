namespace ProjectA.Api.ToDo;

internal static class ToDoMapper
{
    public static ToDoDto ToDto(ToDoItem entity) =>
        new(
            entity.Id,
            entity.Title, 
            entity.Done,
            entity.Category,
            entity.Description,
            entity.DateCreated,
            entity.DateModified
            );

    public static ToDoItem FromDto(ToDoDto dto) =>
        new(dto.id,  dto.category, dto.title, dto.description, dto.date_created, dto.date_modified, dto.actioned);
}
