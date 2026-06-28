namespace ProjectA.Api.ToDo;

public record ToDoItem(
    long Id,
    string Category,
    string Title,
    string? Description,
    DateTime? DateCreated,
    DateTime? DateModified,
    bool Done);

