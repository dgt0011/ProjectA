using ProjectA.Web.Common;
using ProjectA.Web.Models;

namespace ProjectA.Web.Services;

public interface IBlogPostsApiClient
{
    Task<ApiResult<List<BlogPostListItemDto>>> GetListAsync(string status, short postType, string? term, CancellationToken cancellationToken = default);
    Task<ApiResult<BlogPostDto>> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<ApiResult<BlogPostDto>> CreateAsync(BlogPostInput input, CancellationToken cancellationToken = default);
    Task<ApiResult<BlogPostDto>> UpdateAsync(long id, BlogPostInput input, CancellationToken cancellationToken = default);
    Task<ApiResult> DeleteAsync(long id, CancellationToken cancellationToken = default);
    Task<ApiResult<object>> SetPublishedAsync(long id, bool published, CancellationToken cancellationToken = default);
    Task<ApiResult<object>> SetFeaturedAsync(long id, bool featured, CancellationToken cancellationToken = default);
}

public sealed class BlogPostsApiClient(HttpClient httpClient) : ApiClientBase(httpClient), IBlogPostsApiClient
{
    private const string BasePath = "api/blog/posts";

    public Task<ApiResult<List<BlogPostListItemDto>>> GetListAsync(string status, short postType, string? term, CancellationToken cancellationToken = default) =>
        GetJsonAsync<List<BlogPostListItemDto>>(
            $"{BasePath}?status={Uri.EscapeDataString(status)}&type={postType}&term={Uri.EscapeDataString(term ?? string.Empty)}",
            cancellationToken);

    public Task<ApiResult<BlogPostDto>> GetByIdAsync(long id, CancellationToken cancellationToken = default) =>
        GetJsonAsync<BlogPostDto>($"{BasePath}/{id}", cancellationToken);

    public Task<ApiResult<BlogPostDto>> CreateAsync(BlogPostInput input, CancellationToken cancellationToken = default) =>
        PostJsonAsync<BlogPostRequest, BlogPostDto>(BasePath, BlogPostRequest.From(input), cancellationToken);

    public Task<ApiResult<BlogPostDto>> UpdateAsync(long id, BlogPostInput input, CancellationToken cancellationToken = default) =>
        PutJsonAsync<BlogPostRequest, BlogPostDto>($"{BasePath}/{id}", BlogPostRequest.From(input), cancellationToken);

    public Task<ApiResult> DeleteAsync(long id, CancellationToken cancellationToken = default) =>
        DeleteResourceAsync($"{BasePath}/{id}", cancellationToken);

    public Task<ApiResult<object>> SetPublishedAsync(long id, bool published, CancellationToken cancellationToken = default) =>
        PutJsonAsync<FlagRequest, object>($"{BasePath}/{id}/published", new FlagRequest(published), cancellationToken);

    public Task<ApiResult<object>> SetFeaturedAsync(long id, bool featured, CancellationToken cancellationToken = default) =>
        PutJsonAsync<FlagRequest, object>($"{BasePath}/{id}/featured", new FlagRequest(featured), cancellationToken);

    private sealed record FlagRequest(bool Value);
}

public interface IBlogCategoriesApiClient
{
    Task<ApiResult<List<BlogCategoryDto>>> GetListAsync(CancellationToken cancellationToken = default);
    Task<ApiResult<BlogCategoryDto>> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<ApiResult<BlogCategoryDto>> CreateAsync(BlogCategoryInput input, CancellationToken cancellationToken = default);
    Task<ApiResult<BlogCategoryDto>> UpdateAsync(long id, BlogCategoryInput input, CancellationToken cancellationToken = default);
    Task<ApiResult> DeleteAsync(long id, CancellationToken cancellationToken = default);
}

public sealed class BlogCategoriesApiClient(HttpClient httpClient) : ApiClientBase(httpClient), IBlogCategoriesApiClient
{
    private const string BasePath = "api/blog/categories";

    public Task<ApiResult<List<BlogCategoryDto>>> GetListAsync(CancellationToken cancellationToken = default) =>
        GetJsonAsync<List<BlogCategoryDto>>(BasePath, cancellationToken);

    public Task<ApiResult<BlogCategoryDto>> GetByIdAsync(long id, CancellationToken cancellationToken = default) =>
        GetJsonAsync<BlogCategoryDto>($"{BasePath}/{id}", cancellationToken);

    public Task<ApiResult<BlogCategoryDto>> CreateAsync(BlogCategoryInput input, CancellationToken cancellationToken = default) =>
        PostJsonAsync<BlogCategoryInput, BlogCategoryDto>(BasePath, input, cancellationToken);

    public Task<ApiResult<BlogCategoryDto>> UpdateAsync(long id, BlogCategoryInput input, CancellationToken cancellationToken = default) =>
        PutJsonAsync<BlogCategoryInput, BlogCategoryDto>($"{BasePath}/{id}", input, cancellationToken);

    public Task<ApiResult> DeleteAsync(long id, CancellationToken cancellationToken = default) =>
        DeleteResourceAsync($"{BasePath}/{id}", cancellationToken);
}

// Blog-wide admin calls that aren't tied to a single post or category: settings and the
// dashboard's analytics.
public interface IBlogAdminApiClient
{
    Task<ApiResult<BlogSettingsDto>> GetSettingsAsync(CancellationToken cancellationToken = default);
    Task<ApiResult<BlogSettingsDto>> UpdateSettingsAsync(BlogSettingsInput input, CancellationToken cancellationToken = default);
    Task<ApiResult<BlogAnalyticsDto>> GetAnalyticsAsync(int latest, CancellationToken cancellationToken = default);
    Task<ApiResult<BlogDailyViewsDto>> GetDailyViewsAsync(int days, long? postId, CancellationToken cancellationToken = default);
    Task<ApiResult<BlogDailyVisitorsDto>> GetDailyVisitorsAsync(DateOnly day, long? postId, CancellationToken cancellationToken = default);
}

public sealed class BlogAdminApiClient(HttpClient httpClient) : ApiClientBase(httpClient), IBlogAdminApiClient
{
    public Task<ApiResult<BlogSettingsDto>> GetSettingsAsync(CancellationToken cancellationToken = default) =>
        GetJsonAsync<BlogSettingsDto>("api/blog/settings", cancellationToken);

    public Task<ApiResult<BlogSettingsDto>> UpdateSettingsAsync(BlogSettingsInput input, CancellationToken cancellationToken = default) =>
        PutJsonAsync<BlogSettingsInput, BlogSettingsDto>("api/blog/settings", input, cancellationToken);

    public Task<ApiResult<BlogAnalyticsDto>> GetAnalyticsAsync(int latest, CancellationToken cancellationToken = default) =>
        GetJsonAsync<BlogAnalyticsDto>($"api/blog/analytics?latest={latest}", cancellationToken);

    public Task<ApiResult<BlogDailyViewsDto>> GetDailyViewsAsync(int days, long? postId, CancellationToken cancellationToken = default) =>
        GetJsonAsync<BlogDailyViewsDto>(
            $"api/blog/analytics/daily?days={days}" + (postId is { } id ? $"&postId={id}" : ""), cancellationToken);

    public Task<ApiResult<BlogDailyVisitorsDto>> GetDailyVisitorsAsync(DateOnly day, long? postId, CancellationToken cancellationToken = default) =>
        GetJsonAsync<BlogDailyVisitorsDto>(
            $"api/blog/analytics/daily/{day:yyyy-MM-dd}" + (postId is { } id ? $"?postId={id}" : ""), cancellationToken);
}
