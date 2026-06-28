namespace ProjectA.Api.ToDo;

public record ToDoDto(
    // ReSharper disable InconsistentNaming
    long id,
    string title,    
    bool actioned,
    string category,
    string? description,
    DateTime? date_created,
    DateTime? date_modified

    // ReSharper restore InconsistentNaming
    );
