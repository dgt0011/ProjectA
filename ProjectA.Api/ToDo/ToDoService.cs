using Dapper;
using ProjectA.Api.Data;

namespace ProjectA.Api.ToDo;

public class ToDoService(IDbConnectionFactory connectionFactory) : IToDoService
{
    public async Task<List<ToDoItem>> GetList(bool includeDone = false, CancellationToken cancellationToken = default)
    {
        var query = SelectListBaseQuery;
        if (!includeDone)
        {
            query += "WHERE actioned = false";
        }
        query += ";";

        try
        {
            using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
            var dtos = await connection.QueryAsync<ToDoDto>(query);

            return dtos.Select(ToDoMapper.FromDto).ToList();
        }
        catch (Exception ex)
        {
            // TODO: Some logging is necessary
            return new List<ToDoItem>();
        }
    }

    public async Task<ToDoItem?> GetById(uint id, CancellationToken cancellationToken = default)
    {
        var query = SelectListBaseQuery;
        query += $"WHERE id = @Id";

        try
        {
            using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
            var dto = await connection.QueryFirstOrDefaultAsync<ToDoDto>(query, new { Id = (int)id });

            if (dto != null)
            {
                var retVal = ToDoMapper.FromDto(dto);
                return retVal;
            }

            return null;

            //return dto != null ? ToDoMapper.FromDto(dto) : null;
        }
        catch (Exception e)
        {
            // TODO: Some logging is necessary
            return null;
        }
    }

    private const string SelectListBaseQuery = "SELECT " +
                                           "id, title, actioned, category, description, date_created, date_modified " +
                                           "FROM todos ";
}
