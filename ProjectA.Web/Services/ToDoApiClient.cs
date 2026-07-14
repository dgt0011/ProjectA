using ProjectA.Web.Common;
using ProjectA.Web.Models;

namespace ProjectA.Web.Services;

public interface IToDoApiClient
{
    Task<ApiResult<List<ToDoDto>>> GetListAsync(bool includeDone, CancellationToken cancellationToken = default);
    Task<ApiResult<ToDoDto>> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<ApiResult<ToDoDto>> CreateAsync(ToDoCreateInput input, CancellationToken cancellationToken = default);
    Task<ApiResult<ToDoDto>> UpdateAsync(long id, ToDoEditInput input, CancellationToken cancellationToken = default);
    Task<ApiResult> DeleteAsync(long id, CancellationToken cancellationToken = default);
}

public sealed class ToDoApiClient(HttpClient httpClient) : ApiClientBase(httpClient), IToDoApiClient
{
    private const string BasePath = "api/todo";

    public Task<ApiResult<List<ToDoDto>>> GetListAsync(bool includeDone, CancellationToken cancellationToken = default) =>
        GetJsonAsync<List<ToDoDto>>($"{BasePath}?includeDone={(includeDone ? "true" : "false")}", cancellationToken);

    public Task<ApiResult<ToDoDto>> GetByIdAsync(long id, CancellationToken cancellationToken = default) =>
        GetJsonAsync<ToDoDto>($"{BasePath}/{id}", cancellationToken);

    public Task<ApiResult<ToDoDto>> CreateAsync(ToDoCreateInput input, CancellationToken cancellationToken = default) =>
        PostJsonAsync<ToDoCreateInput, ToDoDto>(BasePath, input, cancellationToken);

    public Task<ApiResult<ToDoDto>> UpdateAsync(long id, ToDoEditInput input, CancellationToken cancellationToken = default) =>
        PutJsonAsync<ToDoEditInput, ToDoDto>($"{BasePath}/{id}", input, cancellationToken);

    public Task<ApiResult> DeleteAsync(long id, CancellationToken cancellationToken = default) =>
        DeleteResourceAsync($"{BasePath}/{id}", cancellationToken);
}
