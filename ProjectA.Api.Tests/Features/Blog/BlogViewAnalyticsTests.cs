using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ProjectA.Api.Data;
using ProjectA.Api.Features.Blog.Analytics;
using ProjectA.Api.Features.Blog.Posts;
using ProjectA.Api.Features.Blog.Posts.CreateBlogPost;
using ProjectA.Api.Features.Blog.Public.RecordPostView;
using Xunit;

namespace ProjectA.Api.Tests.Features.Blog;

[Collection(nameof(ApiCollection))]
public class BlogViewAnalyticsTests : IAsyncLifetime
{
    private const string TitlePrefix = "Blogviewtest";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _client;
    private readonly HttpClient _anonymousClient;
    private readonly IDbConnectionFactory _connectionFactory;

    public BlogViewAnalyticsTests(ApiFactory factory)
    {
        _client = factory.CreateAuthenticatedClient();
        _anonymousClient = factory.CreateClient();
        _connectionFactory = factory.Services.GetRequiredService<IDbConnectionFactory>();
    }

    public async Task InitializeAsync() => await CleanUpAsync();

    public async Task DisposeAsync() => await CleanUpAsync();

    // Deleting the posts cascades to their blog_post_views rows.
    private async Task CleanUpAsync()
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        await connection.ExecuteAsync("DELETE FROM blog_posts WHERE title LIKE @Pattern;", new { Pattern = $"{TitlePrefix}%" });
    }

    private async Task<BlogPostResponse> CreateAsync(string title, bool publish = true)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/blog/posts",
            new CreateBlogPostEndpoint.CreateBlogPostRequest(title, null, "Desc", "Body", null, 1, false, publish, null),
            JsonOptions);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<BlogPostResponse>(JsonOptions))!;
    }

    private async Task<HttpResponseMessage> ViewAsync(string slug, string? ip, string? userAgent) =>
        await _anonymousClient.PostAsJsonAsync($"/api/blog/published/{slug}/views", new RecordPostViewRequest(ip, userAgent), JsonOptions);

    [Fact]
    public async Task RecordedViews_AppearInDailyViewsAndVisitors()
    {
        var post = await CreateAsync($"{TitlePrefix} Daily");
        var other = await CreateAsync($"{TitlePrefix} Other");

        Assert.Equal(HttpStatusCode.NoContent, (await ViewAsync(post.Slug, "203.0.113.5", "TestAgent/1.0")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await ViewAsync(post.Slug, "203.0.113.5", "SecondAgent/2.0")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await ViewAsync(post.Slug, "::ffff:198.51.100.7", "OtherAgent")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await ViewAsync(post.Slug, "not-an-ip", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await ViewAsync(other.Slug, "203.0.113.9", "TestAgent/1.0")).StatusCode);

        var reloaded = await _client.GetFromJsonAsync<BlogPostResponse>($"/api/blog/posts/{post.Id}", JsonOptions);
        Assert.Equal(4, reloaded!.PostViews);

        var daily = await _client.GetFromJsonAsync<GetBlogDailyViewsEndpoint.DailyViewsResponse>(
            $"/api/blog/analytics/daily?days=7&postId={post.Id}", JsonOptions);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        Assert.Equal(7, daily!.Days.Count);
        Assert.Equal(today, daily.To);
        Assert.Equal(today.AddDays(-6), daily.From);
        Assert.Equal(today, daily.Days[^1].Day);
        Assert.Equal(4, daily.Days[^1].Views);
        Assert.All(daily.Days.Take(6), d => Assert.Equal(0, d.Views));
        Assert.Equal(4, daily.TotalViews);
        Assert.Equal(4, daily.Posts.Single(p => p.Id == post.Id).Views);
        Assert.Equal(1, daily.Posts.Single(p => p.Id == other.Id).Views);

        var visitors = await _client.GetFromJsonAsync<GetBlogDailyViewsEndpoint.DailyVisitorsResponse>(
            $"/api/blog/analytics/daily/{today:yyyy-MM-dd}?postId={post.Id}", JsonOptions);
        Assert.Equal(4, visitors!.TotalViews);
        Assert.Equal(4, visitors.Visitors.Count);
        Assert.Contains(visitors.Visitors, v => v.IpAddress == "203.0.113.5" && v.UserAgent == "TestAgent/1.0" && v.Views == 1);
        Assert.Contains(visitors.Visitors, v => v.IpAddress == "203.0.113.5" && v.UserAgent == "SecondAgent/2.0");
        Assert.Contains(visitors.Visitors, v => v.IpAddress == "198.51.100.7" && v.UserAgent == "OtherAgent");
        Assert.Contains(visitors.Visitors, v => v.IpAddress is null && v.UserAgent is null);

        // Without postId the whole blog is counted.
        var allVisitors = await _client.GetFromJsonAsync<GetBlogDailyViewsEndpoint.DailyVisitorsResponse>(
            $"/api/blog/analytics/daily/{today:yyyy-MM-dd}", JsonOptions);
        Assert.Contains(allVisitors!.Visitors, v => v.IpAddress == "203.0.113.9" && v.Posts == other.Title);
    }

    [Fact]
    public async Task RecordView_RepeatBySameReaderWithinWindow_CountsOnce()
    {
        var post = await CreateAsync($"{TitlePrefix} Repeat");

        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(HttpStatusCode.NoContent, (await ViewAsync(post.Slug, "203.0.113.20", "Repeat/1.0")).StatusCode);
        }

        // Same reader again after the window has passed: counted.
        using (var connection = await _connectionFactory.CreateConnectionAsync())
        {
            await connection.ExecuteAsync(
                "UPDATE blog_post_views SET viewed_at = viewed_at - interval '31 minutes' WHERE post_id = @Id;", new { post.Id });
        }

        Assert.Equal(HttpStatusCode.NoContent, (await ViewAsync(post.Slug, "203.0.113.20", "Repeat/1.0")).StatusCode);

        var reloaded = await _client.GetFromJsonAsync<BlogPostResponse>($"/api/blog/posts/{post.Id}", JsonOptions);
        Assert.Equal(2, reloaded!.PostViews);
        using var check = await _connectionFactory.CreateConnectionAsync();
        Assert.Equal(2, await check.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM blog_post_views WHERE post_id = @Id;", new { post.Id }));
    }

    [Fact]
    public async Task RecordView_LongUserAgent_IsTruncatedWithoutSplittingSurrogatePair()
    {
        var post = await CreateAsync($"{TitlePrefix} Long agent");
        var userAgent = new string('a', RecordPostViewEndpoint.MaxUserAgentLength - 1) + "😀tail";

        Assert.Equal(HttpStatusCode.NoContent, (await ViewAsync(post.Slug, "203.0.113.30", userAgent)).StatusCode);

        using var connection = await _connectionFactory.CreateConnectionAsync();
        var stored = await connection.ExecuteScalarAsync<string>(
            "SELECT user_agent FROM blog_post_views WHERE post_id = @Id;", new { post.Id });
        Assert.Equal(new string('a', RecordPostViewEndpoint.MaxUserAgentLength - 1), stored);
    }

    [Fact]
    public async Task RetentionService_PurgesOnlyRowsOlderThanRetention()
    {
        var post = await CreateAsync($"{TitlePrefix} Retention");
        Assert.Equal(HttpStatusCode.NoContent, (await ViewAsync(post.Slug, "203.0.113.40", "Old/1.0")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await ViewAsync(post.Slug, "203.0.113.41", "New/1.0")).StatusCode);

        using var connection = await _connectionFactory.CreateConnectionAsync();
        await connection.ExecuteAsync(
            "UPDATE blog_post_views SET viewed_at = now() - interval '400 days' WHERE post_id = @Id AND user_agent = 'Old/1.0';",
            new { post.Id });

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Blog:ViewLogRetentionDays"] = "365" })
            .Build();
        var service = new BlogViewLogRetentionService(
            _connectionFactory, configuration, Microsoft.Extensions.Logging.Abstractions.NullLogger<BlogViewLogRetentionService>.Instance);

        Assert.True(await service.PurgeOnceAsync(CancellationToken.None) >= 1);
        Assert.Equal(["New/1.0"], (await connection.QueryAsync<string>(
            "SELECT user_agent FROM blog_post_views WHERE post_id = @Id;", new { post.Id })).ToList());
    }

    [Fact]
    public async Task RecordView_WithoutBody_StillCounts()
    {
        var post = await CreateAsync($"{TitlePrefix} No body");

        var response = await _anonymousClient.PostAsync($"/api/blog/published/{post.Slug}/views", null);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var connection = await _connectionFactory.CreateConnectionAsync();
        Assert.Equal(1, await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM blog_post_views WHERE post_id = @Id;", new { post.Id }));
    }

    [Fact]
    public async Task RecordView_OfDraft_IsNotFoundAndNotLogged()
    {
        var draft = await CreateAsync($"{TitlePrefix} Draft", publish: false);

        var response = await ViewAsync(draft.Slug, "203.0.113.5", "TestAgent/1.0");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        using var connection = await _connectionFactory.CreateConnectionAsync();
        Assert.Equal(0, await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM blog_post_views WHERE post_id = @Id;", new { draft.Id }));
    }

    [Fact]
    public async Task DailyViews_RequireAuthentication()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await _anonymousClient.GetAsync("/api/blog/analytics/daily")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _anonymousClient.GetAsync("/api/blog/analytics/daily/2026-01-01")).StatusCode);
    }
}
