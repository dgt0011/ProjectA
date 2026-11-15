using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ProjectA.Api.Data;
using ProjectA.Api.Features.Blog.Analytics;
using ProjectA.Api.Features.Blog.Posts;
using ProjectA.Api.Features.Blog.Posts.CreateBlogPost;
using ProjectA.Api.Features.Blog.Public;
using ProjectA.Api.Features.Blog.Public.GetFeedEntries;
using ProjectA.Api.Features.Blog.Public.GetPublishedPostBySlug;
using Xunit;

namespace ProjectA.Api.Tests.Features.Blog;

// Covers the public-site rules the earlier tests didn't: paging past page 1, the
// include_featured rule, newer/related neighbours, feed + sitemap, UTC day boundaries in the
// daily analytics, and author-name privacy.
//
// Posts here get published_at values far in the future (via SQL) so they are always the newest
// on the blog, whatever other tests have left behind.
[Collection(nameof(ApiCollection))]
public class BlogPublicCoverageTests : IAsyncLifetime
{
    private const string TitlePrefix = "Blogcoveragetest";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _client;
    private readonly HttpClient _anonymousClient;
    private readonly IDbConnectionFactory _connectionFactory;

    public BlogPublicCoverageTests(ApiFactory factory)
    {
        _client = factory.CreateAuthenticatedClient();
        _anonymousClient = factory.CreateClient();
        _connectionFactory = factory.Services.GetRequiredService<IDbConnectionFactory>();
    }

    public async Task InitializeAsync() => await CleanUpAsync();

    public async Task DisposeAsync() => await CleanUpAsync();

    private async Task CleanUpAsync()
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        await connection.ExecuteAsync("DELETE FROM blog_posts WHERE title LIKE @Pattern;", new { Pattern = $"{TitlePrefix}%" });
        await connection.ExecuteAsync("DELETE FROM blog_categories WHERE title LIKE @Pattern;", new { Pattern = $"{TitlePrefix}%" });
    }

    // daysAhead orders the posts: a larger value is newer.
    private async Task<BlogPostResponse> CreateAsync(
        string title, int daysAhead, bool featured = false, bool publish = true, IReadOnlyList<string>? categories = null, short postType = 1)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/blog/posts",
            new CreateBlogPostEndpoint.CreateBlogPostRequest(
                $"{TitlePrefix} {title}", null, "Desc", $"Body about {title}", null, postType, featured, publish, categories),
            JsonOptions);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var post = (await response.Content.ReadFromJsonAsync<BlogPostResponse>(JsonOptions))!;

        if (publish)
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();
            await connection.ExecuteAsync(
                "UPDATE blog_posts SET published_at = now() + make_interval(years => 50, days => @Days) WHERE id = @Id;",
                new { Days = daysAhead, post.Id });
        }

        return post;
    }

    private async Task<T> WithSettingsAsync<T>(int itemsPerPage, bool includeFeatured, Func<Task<T>> action)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        var original = await connection.QuerySingleAsync<(int ItemsPerPage, bool IncludeFeatured)>(
            "SELECT items_per_page, include_featured FROM blog_settings ORDER BY id LIMIT 1;");
        await connection.ExecuteAsync(
            "UPDATE blog_settings SET items_per_page = @ItemsPerPage, include_featured = @IncludeFeatured;",
            new { ItemsPerPage = itemsPerPage, IncludeFeatured = includeFeatured });
        try
        {
            return await action();
        }
        finally
        {
            await connection.ExecuteAsync(
                "UPDATE blog_settings SET items_per_page = @ItemsPerPage, include_featured = @IncludeFeatured;",
                new { original.ItemsPerPage, original.IncludeFeatured });
        }
    }

    [Fact]
    public async Task PublishedList_PagesThroughPostsNewestFirst()
    {
        var oldest = await CreateAsync("Paged A", 1);
        var middle = await CreateAsync("Paged B", 2);
        var newest = await CreateAsync("Paged C", 3);

        var (page1, page2) = await WithSettingsAsync(2, true, async () => (
            await _anonymousClient.GetFromJsonAsync<PublishedPostPage>("/api/blog/published?page=1", JsonOptions),
            await _anonymousClient.GetFromJsonAsync<PublishedPostPage>("/api/blog/published?page=2", JsonOptions)));

        Assert.Equal([newest.Id, middle.Id], page1!.Items.Select(p => p.Id));
        Assert.True(page1.HasOlder);
        Assert.False(page1.HasNewer);
        Assert.Equal(oldest.Id, page2!.Items[0].Id);
        Assert.True(page2.HasNewer);
    }

    [Fact]
    public async Task PublishedList_ExcludesFeaturedPosts_WhenIncludeFeaturedIsOff()
    {
        var featured = await CreateAsync("Featured", 2, featured: true);
        var normal = await CreateAsync("Normal", 1);

        var withFeatured = await WithSettingsAsync(10, true, () =>
            _anonymousClient.GetFromJsonAsync<PublishedPostPage>("/api/blog/published", JsonOptions));
        var withoutFeatured = await WithSettingsAsync(10, false, () =>
            _anonymousClient.GetFromJsonAsync<PublishedPostPage>("/api/blog/published", JsonOptions));

        Assert.Contains(withFeatured!.Items, p => p.Id == featured.Id);
        Assert.DoesNotContain(withoutFeatured!.Items, p => p.Id == featured.Id);
        Assert.Contains(withoutFeatured.Items, p => p.Id == normal.Id);
    }

    [Fact]
    public async Task PostBySlug_HasNewerNeighbourAndRelatedPosts_PagesHaveNeither()
    {
        var older = await CreateAsync("Wombat care", 1, categories: [$"{TitlePrefix}Marsupials"]);
        var current = await CreateAsync("Wombat burrows", 2, categories: [$"{TitlePrefix}Marsupials"]);
        var newer = await CreateAsync("Unrelated gardening", 3);
        var page = await CreateAsync("Wombat page", 4, postType: 2);

        var detail = await _anonymousClient.GetFromJsonAsync<GetPublishedPostBySlugEndpoint.PublishedPostResponse>(
            $"/api/blog/published/{current.Slug}", JsonOptions);

        Assert.Equal(older.Slug, detail!.Older!.Slug);
        Assert.Equal(newer.Slug, detail.Newer!.Slug);
        Assert.Equal(older.Id, detail.Related[0].Id);
        Assert.DoesNotContain(detail.Related, r => r.Id == current.Id || r.Id == page.Id);

        var pageDetail = await _anonymousClient.GetFromJsonAsync<GetPublishedPostBySlugEndpoint.PublishedPostResponse>(
            $"/api/blog/published/{page.Slug}", JsonOptions);
        Assert.Null(pageDetail!.Older);
        Assert.Null(pageDetail.Newer);
        Assert.Empty(pageDetail.Related);
    }

    [Fact]
    public async Task FeedAndSitemap_ListPublishedContentOnly()
    {
        var post = await CreateAsync("In the feed", 2);
        var page = await CreateAsync("Sitemap page", 1, postType: 2);
        var draft = await CreateAsync("Draft", 0, publish: false);

        var feed = await _anonymousClient.GetFromJsonAsync<List<GetFeedEntriesEndpoint.FeedEntryResponse>>("/api/blog/feed", JsonOptions);
        Assert.Contains(feed!, e => e.Slug == post.Slug);
        Assert.DoesNotContain(feed!, e => e.Slug == page.Slug || e.Slug == draft.Slug);

        var sitemap = await _anonymousClient.GetFromJsonAsync<List<GetFeedEntriesEndpoint.SitemapEntryResponse>>("/api/blog/sitemap", JsonOptions);
        Assert.Contains(sitemap!, e => e.Slug == post.Slug && e.PostType == 1);
        Assert.Contains(sitemap!, e => e.Slug == page.Slug && e.PostType == 2);
        Assert.DoesNotContain(sitemap!, e => e.Slug == draft.Slug);
    }

    [Fact]
    public async Task DailyViews_SplitOnUtcMidnight()
    {
        var post = await CreateAsync("Midnight", 1);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var midnight = DateTime.SpecifyKind(today.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);

        using (var connection = await _connectionFactory.CreateConnectionAsync())
        {
            await connection.ExecuteAsync(
                "INSERT INTO blog_post_views (post_id, viewed_at) VALUES (@Id, @Before), (@Id, @After);",
                new { post.Id, Before = midnight.AddSeconds(-1), After = midnight });
        }

        var daily = await _client.GetFromJsonAsync<GetBlogDailyViewsEndpoint.DailyViewsResponse>(
            $"/api/blog/analytics/daily?days=2&postId={post.Id}", JsonOptions);

        Assert.Equal([(today.AddDays(-1), 1), (today, 1)], daily!.Days.Select(d => (d.Day, d.Views)));

        var yesterday = await _client.GetFromJsonAsync<GetBlogDailyViewsEndpoint.DailyVisitorsResponse>(
            $"/api/blog/analytics/daily/{today.AddDays(-1):yyyy-MM-dd}?postId={post.Id}", JsonOptions);
        Assert.Equal(1, yesterday!.TotalViews);
    }

    [Fact]
    public async Task AuthorName_IsNeverTheLoginUsername()
    {
        var post = await CreateAsync("Anonymous author", 1);

        using var connection = await _connectionFactory.CreateConnectionAsync();
        var author = await connection.QuerySingleAsync<(long Id, string Username, string? DisplayName)>(
            "SELECT u.id, u.username, u.display_name FROM users u JOIN blog_posts p ON p.author_id = u.id WHERE p.id = @Id;",
            new { post.Id });
        await connection.ExecuteAsync("UPDATE users SET display_name = NULL WHERE id = @Id;", new { author.Id });
        try
        {
            var detail = await _anonymousClient.GetFromJsonAsync<GetPublishedPostBySlugEndpoint.PublishedPostResponse>(
                $"/api/blog/published/{post.Slug}", JsonOptions);
            Assert.Equal(string.Empty, detail!.Post.AuthorName);

            var feedJson = await _anonymousClient.GetStringAsync("/api/blog/feed");
            Assert.DoesNotContain($"\"authorName\":\"{author.Username}\"", feedJson);
        }
        finally
        {
            await connection.ExecuteAsync(
                "UPDATE users SET display_name = @DisplayName WHERE id = @Id;", new { author.Id, author.DisplayName });
        }
    }
}
