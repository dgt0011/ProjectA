using ProjectA.Web.Common;
using ProjectA.Web.Models;

namespace ProjectA.Web.Services;

public interface IToDoApiClient
{
    Task<ApiResult<List<ToDoDto>>> GetListAsync(bool includeDone, long? projectId = null, CancellationToken cancellationToken = default);
    Task<ApiResult<ToDoDto>> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<ApiResult<ToDoDto>> CreateAsync(ToDoCreateInput input, CancellationToken cancellationToken = default);
    Task<ApiResult<ToDoDto>> UpdateAsync(long id, ToDoEditInput input, CancellationToken cancellationToken = default);
    Task<ApiResult<ToDoDto>> CompleteAsync(long id, ToDoCompleteInput input, CancellationToken cancellationToken = default);
    Task<ApiResult> DeleteAsync(long id, CancellationToken cancellationToken = default);
}

public sealed class ToDoApiClient(HttpClient httpClient) : ApiClientBase(httpClient), IToDoApiClient
{
    private const string BasePath = "api/todo";

    public Task<ApiResult<List<ToDoDto>>> GetListAsync(bool includeDone, long? projectId = null, CancellationToken cancellationToken = default)
    {
        var query = $"includeDone={(includeDone ? "true" : "false")}";
        if (projectId is not null)
        {
            query += $"&projectId={projectId}";
        }

        return GetJsonAsync<List<ToDoDto>>($"{BasePath}?{query}", cancellationToken);
    }

    public Task<ApiResult<ToDoDto>> GetByIdAsync(long id, CancellationToken cancellationToken = default) =>
        GetJsonAsync<ToDoDto>($"{BasePath}/{id}", cancellationToken);

    public Task<ApiResult<ToDoDto>> CreateAsync(ToDoCreateInput input, CancellationToken cancellationToken = default) =>
        PostJsonAsync<ToDoCreateInput, ToDoDto>(BasePath, input, cancellationToken);

    public Task<ApiResult<ToDoDto>> UpdateAsync(long id, ToDoEditInput input, CancellationToken cancellationToken = default) =>
        PutJsonAsync<ToDoEditInput, ToDoDto>($"{BasePath}/{id}", input, cancellationToken);

    public Task<ApiResult<ToDoDto>> CompleteAsync(long id, ToDoCompleteInput input, CancellationToken cancellationToken = default) =>
        PutJsonAsync<ToDoCompleteInput, ToDoDto>($"{BasePath}/{id}/complete", input, cancellationToken);

    public Task<ApiResult> DeleteAsync(long id, CancellationToken cancellationToken = default) =>
        DeleteResourceAsync($"{BasePath}/{id}", cancellationToken);
}
