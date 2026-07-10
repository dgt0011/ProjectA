using ProjectA.Web.Common;
using ProjectA.Web.Models;

namespace ProjectA.Web.Services;

public interface IBookmarksApiClient
{
    Task<ApiResult<List<BookmarkDto>>> GetListAsync(CancellationToken cancellationToken = default);
    Task<ApiResult<BookmarkDto>> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<ApiResult<BookmarkDto>> CreateAsync(BookmarkInput input, CancellationToken cancellationToken = default);
    Task<ApiResult<BookmarkDto>> UpdateAsync(long id, BookmarkInput input, CancellationToken cancellationToken = default);
    Task<ApiResult> DeleteAsync(long id, CancellationToken cancellationToken = default);
}

public sealed class BookmarksApiClient(HttpClient httpClient) : ApiClientBase(httpClient), IBookmarksApiClient
{
    private const string BasePath = "api/bookmarks";

    public Task<ApiResult<List<BookmarkDto>>> GetListAsync(CancellationToken cancellationToken = default) =>
        GetJsonAsync<List<BookmarkDto>>(BasePath, cancellationToken);

    public Task<ApiResult<BookmarkDto>> GetByIdAsync(long id, CancellationToken cancellationToken = default) =>
        GetJsonAsync<BookmarkDto>($"{BasePath}/{id}", cancellationToken);

    public Task<ApiResult<BookmarkDto>> CreateAsync(BookmarkInput input, CancellationToken cancellationToken = default) =>
        PostJsonAsync<BookmarkInput, BookmarkDto>(BasePath, input, cancellationToken);

    public Task<ApiResult<BookmarkDto>> UpdateAsync(long id, BookmarkInput input, CancellationToken cancellationToken = default) =>
        PutJsonAsync<BookmarkInput, BookmarkDto>($"{BasePath}/{id}", input, cancellationToken);

    public Task<ApiResult> DeleteAsync(long id, CancellationToken cancellationToken = default) =>
        DeleteResourceAsync($"{BasePath}/{id}", cancellationToken);
}
