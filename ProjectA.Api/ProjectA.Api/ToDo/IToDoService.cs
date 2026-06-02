namespace ProjectA.Api.ToDo;

public interface IToDoService
{
    /// <summary>
    /// Retrieve the set of all outstanding ToDos.  Optionally include completed ToDos by passing true to <paramref name="includeDone"/>
    /// </summary>
    /// <returns>A list of ToDo records.
    /// If <paramref name="includeDone"/> is true, this list includes completed ToDo records.
    /// </returns>
    Task<List<ToDo>> GetList(bool includeDone = false);
    
    /// <summary>
    /// Retrieve a ToDo by its Id.
    /// </summary>
    /// <param name="id">The Id of the ToDo to retrieve</param>
    /// <returns>If the Id does not exist, then returns null,
    /// otherwise returns the ToDo record.
    /// Note that this is regardless of whether the ToDo has been completed.</returns>
    Task<ToDo?> GetById(uint id);
}