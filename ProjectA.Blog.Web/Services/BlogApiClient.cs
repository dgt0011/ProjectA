using Microsoft.Extensions.Caching.Memory;
using ProjectA.Blog.Web.Common;
using ProjectA.Blog.Web.Models;

namespace ProjectA.Blog.Web.Services;

// The public blog only ever calls ProjectA.Api's anonymous /api/blog endpoints - it holds no
// credentials and has no way to see drafts or change anything except the view counter.
public interface IBlogApiClient
{
    Task<BlogSite> GetSiteAsync(CancellationToken cancellationToken = default);
    Task<ApiResult<PostPage>> GetPublishedAsync(int page, string? category, CancellationToken cancellationToken = default);
    Task<ApiResult<PostPage>> SearchAsync(string term, int page, CancellationToken cancellationToken = default);
    Task<ApiResult<PostDetail>> GetPostAsync(string slug, CancellationToken cancellationToken = default);
    Task RecordViewAsync(string slug, CancellationToken cancellationToken = default);
    Task<ApiResult<List<FeedEntry>>> GetFeedAsync(CancellationToken cancellationToken = default);
    Task<ApiResult<List<SitemapEntry>>> GetSitemapAsync(CancellationToken cancellationToken = default);
}

public sealed class BlogApiClient(HttpClient httpClient, IMemoryCache cache, ILogger<BlogApiClient> logger)
    : ApiClientBase(httpClient), IBlogApiClient
{
    private const string SiteCacheKey = "blog-site";

    // The layout needs the site data (title, menu categories, header/footer scripts) on every
    // request; a short cache keeps that from doubling every page's API calls while still
    // picking up admin changes within a minute.
    private static readonly TimeSpan SiteCacheDuration = TimeSpan.FromSeconds(60);

    public async Task<BlogSite> GetSiteAsync(CancellationToken cancellationToken = default)
    {
        if (cache.TryGetValue(SiteCacheKey, out BlogSite? cached) && cached is not null)
        {
            return cached;
        }

        var result = await GetJsonAsync<BlogSite>("api/blog/site", cancellationToken);
        if (result is { IsSuccess: true, Value: not null })
        {
            cache.Set(SiteCacheKey, result.Value, SiteCacheDuration);
            return result.Value;
        }

        logger.LogWarning("Could not load blog site data ({Status}): {Message}", result.StatusCode, result.ToDisplayMessage("unknown error"));
        return BlogSite.Unavailable;
    }

    public Task<ApiResult<PostPage>> GetPublishedAsync(int page, string? category, CancellationToken cancellationToken = default) =>
        GetJsonAsync<PostPage>(
            $"api/blog/published?page={page}" + (string.IsNullOrWhiteSpace(category) ? "" : $"&category={Uri.EscapeDataString(category)}"),
            cancellationToken);

    public Task<ApiResult<PostPage>> SearchAsync(string term, int page, CancellationToken cancellationToken = default) =>
        GetJsonAsync<PostPage>($"api/blog/search?term={Uri.EscapeDataString(term)}&page={page}", cancellationToken);

    public Task<ApiResult<PostDetail>> GetPostAsync(string slug, CancellationToken cancellationToken = default) =>
        GetJsonAsync<PostDetail>($"api/blog/published/{Uri.EscapeDataString(slug)}", cancellationToken);

    public async Task RecordViewAsync(string slug, CancellationToken cancellationToken = default)
    {
        // Best effort - a failed view count must never break rendering the post.
        var result = await PostJsonAsync<object?, object>($"api/blog/published/{Uri.EscapeDataString(slug)}/views", null, cancellationToken);
        if (!result.IsSuccess)
        {
            logger.LogDebug("Could not record a view for {Slug} ({Status})", slug, result.StatusCode);
        }
    }

    public Task<ApiResult<List<FeedEntry>>> GetFeedAsync(CancellationToken cancellationToken = default) =>
        GetJsonAsync<List<FeedEntry>>("api/blog/feed", cancellationToken);

    public Task<ApiResult<List<SitemapEntry>>> GetSitemapAsync(CancellationToken cancellationToken = default) =>
        GetJsonAsync<List<SitemapEntry>>("api/blog/sitemap", cancellationToken);
}
