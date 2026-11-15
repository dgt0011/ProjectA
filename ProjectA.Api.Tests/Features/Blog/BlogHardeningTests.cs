using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ProjectA.Api.Data;
using ProjectA.Api.Features.Blog.Analytics;
using ProjectA.Api.Features.Blog.Categories;
using ProjectA.Api.Features.Blog.Posts;
using ProjectA.Api.Features.Blog.Posts.CreateBlogPost;
using ProjectA.Api.Features.Blog.Posts.UpdateBlogPost;
using ProjectA.Api.Features.Blog.Public;
using ProjectA.Api.Features.Blog.Public.GetBlogSite;
using ProjectA.Api.Features.Blog.Public.RecordPostView;
using Xunit;

namespace ProjectA.Api.Tests.Features.Blog;

// Regression tests for the fixes from the blog security/bug review.
[Collection(nameof(ApiCollection))]
public class BlogHardeningTests : IAsyncLifetime
{
    private const string TitlePrefix = "Bloghardentest";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _client;
    private readonly HttpClient _anonymousClient;
    private readonly IDbConnectionFactory _connectionFactory;

    public BlogHardeningTests(ApiFactory factory)
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
        await connection.ExecuteAsync(
            "DELETE FROM blog_posts WHERE title LIKE @Pattern OR slug IN ('search', 'search2');",
            new { Pattern = $"{TitlePrefix}%" });
        await connection.ExecuteAsync("DELETE FROM blog_categories WHERE title ILIKE @Pattern;", new { Pattern = $"{TitlePrefix}%" });
    }

    private Task<HttpResponseMessage> PostAsync(string title, string? slug = null, short postType = 1, IReadOnlyList<string>? categories = null) =>
        _client.PostAsJsonAsync(
            "/api/blog/posts",
            new CreateBlogPostEndpoint.CreateBlogPostRequest(title, slug, "Desc", "Body", null, postType, false, true, categories),
            JsonOptions);

    private async Task<BlogPostResponse> CreateAsync(string title, string? slug = null, short postType = 1, IReadOnlyList<string>? categories = null)
    {
        var response = await PostAsync(title, slug, postType, categories);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<BlogPostResponse>(JsonOptions))!;
    }

    [Fact]
    public async Task ReservedSlug_IsRejected_AndNeverGenerated()
    {
        var explicitSlug = await PostAsync($"{TitlePrefix} Explicit", slug: "posts", postType: 2);
        Assert.Equal(HttpStatusCode.BadRequest, explicitSlug.StatusCode);

        // A page titled "Search" would get the slug "search", which /search on the public site
        // shadows - it gets the next free variant instead.
        var page = await CreateAsync("Search", postType: 2);
        Assert.Equal("search2", page.Slug);
    }

    [Fact]
    public async Task HugePageNumber_ReturnsEmptyPage_NotServerError()
    {
        var list = await _anonymousClient.GetAsync($"/api/blog/published?page={int.MaxValue}");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var page = await list.Content.ReadFromJsonAsync<PublishedPostPage>(JsonOptions);
        Assert.Empty(page!.Items);

        var search = await _anonymousClient.GetAsync($"/api/blog/search?term=body&page={int.MaxValue}");
        Assert.Equal(HttpStatusCode.OK, search.StatusCode);
        Assert.Empty((await search.Content.ReadFromJsonAsync<PublishedPostPage>(JsonOptions))!.Items);
    }

    [Fact]
    public async Task Search_WithManyLongTerms_StillRanksFirstTerms()
    {
        var post = await CreateAsync($"{TitlePrefix} Quokkas everywhere");
        var term = "quokkas " + string.Join(' ', Enumerable.Range(0, 2000).Select(i => $"zz{i:D4}"));

        var page = await _anonymousClient.GetFromJsonAsync<PublishedPostPage>(
            $"/api/blog/search?term={Uri.EscapeDataString(term)}", JsonOptions);

        Assert.Contains(page!.Items, p => p.Id == post.Id);
    }

    [Fact]
    public async Task CategoryTitles_WithCommas_AreRejected()
    {
        var category = await _client.PostAsJsonAsync(
            "/api/blog/categories", new BlogCategoryEndpoints.BlogCategoryRequest($"{TitlePrefix} a, b", null), JsonOptions);
        Assert.Equal(HttpStatusCode.BadRequest, category.StatusCode);

        var post = await PostAsync($"{TitlePrefix} Comma", categories: [$"{TitlePrefix} a, b"]);
        Assert.Equal(HttpStatusCode.BadRequest, post.StatusCode);

        var nullCategory = await _client.PostAsync(
            "/api/blog/posts",
            JsonContent.Create(new { title = $"{TitlePrefix} Null cat", postType = 1, categories = new string?[] { "x", null } }));
        Assert.Equal(HttpStatusCode.BadRequest, nullCategory.StatusCode);
    }

    [Fact]
    public async Task Update_KeepsViewsRecordedSinceTheEditorLoaded()
    {
        var post = await CreateAsync($"{TitlePrefix} Views kept");
        await _anonymousClient.PostAsJsonAsync(
            $"/api/blog/published/{post.Slug}/views", new RecordPostViewRequest("203.0.113.50", "Keep/1.0"), JsonOptions);

        var update = new UpdateBlogPostEndpoint.UpdateBlogPostRequest(
            $"{TitlePrefix} Views kept (edited)", post.Slug, "Desc", "Body", null, 1, false, true, null);
        var response = await _client.PutAsJsonAsync($"/api/blog/posts/{post.Id}", update, JsonOptions);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<BlogPostResponse>(JsonOptions);

        Assert.Equal(1, updated!.PostViews);
        Assert.Equal(post.PublishedAt, updated.PublishedAt);
        Assert.Equal($"{TitlePrefix} Views kept (edited)", updated.Title);
    }

    [Fact]
    public async Task SiteCategories_CountOnlyPosts()
    {
        await CreateAsync($"{TitlePrefix} About", postType: 2, categories: [$"{TitlePrefix}PageOnly"]);
        await CreateAsync($"{TitlePrefix} Article", categories: [$"{TitlePrefix}Mixed"]);
        await CreateAsync($"{TitlePrefix} Contact", postType: 2, categories: [$"{TitlePrefix}Mixed"]);

        var site = await _anonymousClient.GetFromJsonAsync<GetBlogSiteEndpoint.BlogSiteResponse>("/api/blog/site", JsonOptions);

        Assert.DoesNotContain(site!.Categories, c => c.Title == $"{TitlePrefix}PageOnly");
        Assert.Equal(1, site.Categories.Single(c => c.Title == $"{TitlePrefix}Mixed").PostCount);
    }

    [Fact]
    public async Task DailyVisitors_ReportsWholeDayTotals()
    {
        var post = await CreateAsync($"{TitlePrefix} Visitors");
        await _anonymousClient.PostAsJsonAsync(
            $"/api/blog/published/{post.Slug}/views", new RecordPostViewRequest("203.0.113.60", "A/1.0"), JsonOptions);
        await _anonymousClient.PostAsJsonAsync(
            $"/api/blog/published/{post.Slug}/views", new RecordPostViewRequest(null, null), JsonOptions);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var visitors = await _client.GetFromJsonAsync<GetBlogDailyViewsEndpoint.DailyVisitorsResponse>(
            $"/api/blog/analytics/daily/{today:yyyy-MM-dd}?postId={post.Id}", JsonOptions);

        Assert.Equal(2, visitors!.TotalViews);
        Assert.Equal(2, visitors.TotalVisitors);
        Assert.Equal(2, visitors.Visitors.Count);
    }
}
