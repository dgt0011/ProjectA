using ProjectA.Web.Common;
using ProjectA.Web.Models;

namespace ProjectA.Web.Services;

public interface IUsersApiClient
{
    Task<ApiResult<List<UserDto>>> GetListAsync(CancellationToken cancellationToken = default);
    Task<ApiResult<UserDto>> CreateAsync(UserInput input, CancellationToken cancellationToken = default);
    Task<ApiResult> DeleteAsync(long id, CancellationToken cancellationToken = default);

    // Self-service - always changes whichever account the current bearer token (attached by
    // BearerTokenHandler) belongs to. There is no id parameter here on purpose.
    Task<ApiResult<ChangePasswordResult>> ChangePasswordAsync(ChangePasswordInput input, CancellationToken cancellationToken = default);
}

// Every /api/users route requires an authenticated caller (see UserEndpointsModule on the
// API side), so unlike the other entity clients there is no anonymous read path here at all.
public sealed class UsersApiClient(HttpClient httpClient) : ApiClientBase(httpClient), IUsersApiClient
{
    private const string BasePath = "api/users";

    public Task<ApiResult<List<UserDto>>> GetListAsync(CancellationToken cancellationToken = default) =>
        GetJsonAsync<List<UserDto>>(BasePath, cancellationToken);

    public Task<ApiResult<UserDto>> CreateAsync(UserInput input, CancellationToken cancellationToken = default) =>
        PostJsonAsync<UserInput, UserDto>(BasePath, input, cancellationToken);

    public Task<ApiResult> DeleteAsync(long id, CancellationToken cancellationToken = default) =>
        DeleteResourceAsync($"{BasePath}/{id}", cancellationToken);

    public Task<ApiResult<ChangePasswordResult>> ChangePasswordAsync(ChangePasswordInput input, CancellationToken cancellationToken = default) =>
        PostJsonAsync<ChangePasswordInput, ChangePasswordResult>($"{BasePath}/change-password", input, cancellationToken);
}
