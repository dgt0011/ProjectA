namespace ProjectA.Api.ToDo;

public class ToDoService : IToDoService
{
    private readonly List<ToDo> _testingTodos;
    
    public ToDoService()
    {
        // for now fake the connection to the postgresql database and instead
        // create a set of ToDo records to act as the database data
        _testingTodos =
        [
            new ToDo(1, 1, "Test 1 ToDo", "Test 1 Description", DateTime.Now, DateTime.MinValue, false),
            new ToDo(2, 1, "Test 2 ToDo", "Test 2 Description", DateTime.Now, DateTime.MinValue, false),
            new ToDo(3, 1, "Test 3 ToDo", "Test 3 Description", DateTime.Now, DateTime.MinValue, true)
        ];
    }
    
    public async Task<List<ToDo>> GetList(bool includeDone = false)
    {
        if (includeDone)
        {
            return await Task.FromResult(_testingTodos);
        }
        return await Task.FromResult(_testingTodos.FindAll(a=>!a.Done));
    }

    public async Task<ToDo?> GetById(uint id)
    {
        return await Task.FromResult(_testingTodos.Find(a => a.Id == id));
    }
}