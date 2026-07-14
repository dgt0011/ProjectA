using ProjectA.Web.Common;
using ProjectA.Web.Models;

namespace ProjectA.Web.Services;

public interface IBookmarkTypesApiClient
{
    Task<ApiResult<List<BookmarkTypeDto>>> GetListAsync(CancellationToken cancellationToken = default);
    Task<ApiResult<BookmarkTypeDto>> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<ApiResult<BookmarkTypeDto>> CreateAsync(BookmarkTypeInput input, CancellationToken cancellationToken = default);
    Task<ApiResult<BookmarkTypeDto>> UpdateAsync(long id, BookmarkTypeInput input, CancellationToken cancellationToken = default);
    Task<ApiResult> DeleteAsync(long id, CancellationToken cancellationToken = default);

    // Not JSON, so this doesn't go through ApiClientBase's Get/Post/PutJsonAsync helpers -
    // used by the Web app's own icon-proxy route (see Program.cs) to stream bytes to <img>
    // tags in the browser, which can't call ProjectA.Api directly.
    Task<BookmarkTypeIcon?> GetIconAsync(long id, CancellationToken cancellationToken = default);
}

public sealed record BookmarkTypeIcon(byte[] Bytes, string ContentType);

public sealed class BookmarkTypesApiClient(HttpClient httpClient) : ApiClientBase(httpClient), IBookmarkTypesApiClient
{
    private const string BasePath = "api/bookmarktypes";

    public Task<ApiResult<List<BookmarkTypeDto>>> GetListAsync(CancellationToken cancellationToken = default) =>
        GetJsonAsync<List<BookmarkTypeDto>>(BasePath, cancellationToken);

    public Task<ApiResult<BookmarkTypeDto>> GetByIdAsync(long id, CancellationToken cancellationToken = default) =>
        GetJsonAsync<BookmarkTypeDto>($"{BasePath}/{id}", cancellationToken);

    public Task<ApiResult<BookmarkTypeDto>> CreateAsync(BookmarkTypeInput input, CancellationToken cancellationToken = default) =>
        PostJsonAsync<BookmarkTypeInput, BookmarkTypeDto>(BasePath, input, cancellationToken);

    public Task<ApiResult<BookmarkTypeDto>> UpdateAsync(long id, BookmarkTypeInput input, CancellationToken cancellationToken = default) =>
        PutJsonAsync<BookmarkTypeInput, BookmarkTypeDto>($"{BasePath}/{id}", input, cancellationToken);

    public Task<ApiResult> DeleteAsync(long id, CancellationToken cancellationToken = default) =>
        DeleteResourceAsync($"{BasePath}/{id}", cancellationToken);

    public async Task<BookmarkTypeIcon?> GetIconAsync(long id, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await HttpClient.GetAsync($"{BasePath}/{id}/icon", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
            return new BookmarkTypeIcon(bytes, contentType);
        }
        catch (HttpRequestException)
        {
            // Same "API unreachable is routine, not a bug" reasoning as ApiClientBase.GuardAsync
            // - the icon-proxy route just returns a 404 to the browser in that case.
            return null;
        }
    }
}
