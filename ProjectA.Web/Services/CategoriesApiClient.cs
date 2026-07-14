using ProjectA.Web.Common;
using ProjectA.Web.Models;

namespace ProjectA.Web.Services;

public interface ICategoriesApiClient
{
    Task<ApiResult<List<CategoryDto>>> GetListAsync(CancellationToken cancellationToken = default);
    Task<ApiResult<CategoryDto>> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<ApiResult<CategoryDto>> CreateAsync(CategoryInput input, CancellationToken cancellationToken = default);
    Task<ApiResult<CategoryDto>> UpdateAsync(long id, CategoryInput input, CancellationToken cancellationToken = default);
    Task<ApiResult> DeleteAsync(long id, CancellationToken cancellationToken = default);
}

public sealed class CategoriesApiClient(HttpClient httpClient) : ApiClientBase(httpClient), ICategoriesApiClient
{
    private const string BasePath = "api/categories";

    public Task<ApiResult<List<CategoryDto>>> GetListAsync(CancellationToken cancellationToken = default) =>
        GetJsonAsync<List<CategoryDto>>(BasePath, cancellationToken);

    public Task<ApiResult<CategoryDto>> GetByIdAsync(long id, CancellationToken cancellationToken = default) =>
        GetJsonAsync<CategoryDto>($"{BasePath}/{id}", cancellationToken);

    public Task<ApiResult<CategoryDto>> CreateAsync(CategoryInput input, CancellationToken cancellationToken = default) =>
        PostJsonAsync<CategoryInput, CategoryDto>(BasePath, input, cancellationToken);

    public Task<ApiResult<CategoryDto>> UpdateAsync(long id, CategoryInput input, CancellationToken cancellationToken = default) =>
        PutJsonAsync<CategoryInput, CategoryDto>($"{BasePath}/{id}", input, cancellationToken);

    public Task<ApiResult> DeleteAsync(long id, CancellationToken cancellationToken = default) =>
        DeleteResourceAsync($"{BasePath}/{id}", cancellationToken);
}
