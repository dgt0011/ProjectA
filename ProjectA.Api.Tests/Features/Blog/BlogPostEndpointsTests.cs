using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ProjectA.Api.Data;
using ProjectA.Api.Features.Blog.Posts;
using ProjectA.Api.Features.Blog.Posts.CreateBlogPost;
using ProjectA.Api.Features.Blog.Posts.GetBlogPostList;
using ProjectA.Api.Features.Blog.Posts.UpdateBlogPost;
using ProjectA.Api.Features.Blog.Public;
using ProjectA.Api.Features.Blog.Public.GetPublishedPostBySlug;
using Xunit;

namespace ProjectA.Api.Tests.Features.Blog;

[Collection(nameof(ApiCollection))]
public class BlogPostEndpointsTests : IAsyncLifetime
{
    private const string TitlePrefix = "Blogtest";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _client;
    private readonly HttpClient _anonymousClient;
    private readonly IDbConnectionFactory _connectionFactory;

    public BlogPostEndpointsTests(ApiFactory factory)
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

    private async Task<BlogPostResponse> CreateAsync(
        string title, bool publish = true, string? slug = null, string content = "Body", IReadOnlyList<string>? categories = null, short postType = 1)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/blog/posts",
            new CreateBlogPostEndpoint.CreateBlogPostRequest(title, slug, "Desc", content, null, postType, false, publish, categories),
            JsonOptions);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<BlogPostResponse>(JsonOptions))!;
    }

    [Fact]
    public async Task Create_WithoutSlug_GeneratesUniqueSlugFromTitle()
    {
        var first = await CreateAsync($"{TitlePrefix} Hello World!");
        var second = await CreateAsync($"{TitlePrefix} Hello World!");

        Assert.Equal("blogtest-hello-world", first.Slug);
        Assert.Equal("blogtest-hello-world2", second.Slug);
        Assert.NotNull(first.PublishedAt);
        Assert.NotNull(first.AuthorId);
    }

    [Fact]
    public async Task Create_WithTakenSlug_ReturnsValidationProblem()
    {
        await CreateAsync($"{TitlePrefix} Original", slug: "blogtest-taken");

        var response = await _client.PostAsJsonAsync(
            "/api/blog/posts",
            new CreateBlogPostEndpoint.CreateBlogPostRequest($"{TitlePrefix} Clash", "blogtest-taken", null, null, null),
            JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _anonymousClient.PostAsJsonAsync(
            "/api/blog/posts",
            new CreateBlogPostEndpoint.CreateBlogPostRequest($"{TitlePrefix} Anon", null, null, null, null),
            JsonOptions);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Drafts_AreHiddenFromPublicEndpoints_ButListedForAdmins()
    {
        var draft = await CreateAsync($"{TitlePrefix} Draft", publish: false);

        var publicResponse = await _anonymousClient.GetAsync($"/api/blog/published/{draft.Slug}");
        Assert.Equal(HttpStatusCode.NotFound, publicResponse.StatusCode);

        var adminList = await _client.GetFromJsonAsync<List<GetBlogPostListEndpoint.BlogPostListItemResponse>>(
            "/api/blog/posts?status=drafts", JsonOptions);
        Assert.Contains(adminList!, p => p.Id == draft.Id);
    }

    [Fact]
    public async Task PublishedPost_IsReturnedBySlug_WithCategoriesAndNeighbours()
    {
        var older = await CreateAsync($"{TitlePrefix} Older post");
        await Task.Delay(20);
        var post = await CreateAsync($"{TitlePrefix} Middle post", categories: [$"{TitlePrefix}Cat", $"{TitlePrefix}cat"]);

        var response = await _anonymousClient.GetFromJsonAsync<GetPublishedPostBySlugEndpoint.PublishedPostResponse>(
            $"/api/blog/published/{post.Slug}", JsonOptions);

        Assert.NotNull(response);
        Assert.Equal("Body", response.Content);
        // Category titles are de-duplicated case-insensitively.
        Assert.Single(response.Post.Categories);
        Assert.Equal(older.Slug, response.Older?.Slug);
    }

    [Fact]
    public async Task PublishedList_FiltersByCategory_CaseInsensitively()
    {
        var tagged = await CreateAsync($"{TitlePrefix} Tagged", categories: [$"{TitlePrefix}Topic"]);
        await CreateAsync($"{TitlePrefix} Untagged");

        var page = await _anonymousClient.GetFromJsonAsync<PublishedPostPage>(
            $"/api/blog/published?category={TitlePrefix.ToUpperInvariant()}TOPIC", JsonOptions);

        Assert.NotNull(page);
        Assert.Equal(tagged.Id, Assert.Single(page.Items).Id);
    }

    [Fact]
    public async Task Search_RanksTitleMatchesFirst()
    {
        var contentOnly = await CreateAsync($"{TitlePrefix} Something else", content: "zebrafinch zebrafinch");
        var titleMatch = await CreateAsync($"{TitlePrefix} Zebrafinch notes");

        var page = await _anonymousClient.GetFromJsonAsync<PublishedPostPage>("/api/blog/search?term=zebrafinch", JsonOptions);

        Assert.NotNull(page);
        Assert.Equal([titleMatch.Id, contentOnly.Id], page.Items.Select(i => i.Id).ToList());
    }

    [Fact]
    public async Task Update_PublishingKeepsOriginalPublishDate_AndUnpublishingMakesDraft()
    {
        var post = await CreateAsync($"{TitlePrefix} Update me");

        var update = new UpdateBlogPostEndpoint.UpdateBlogPostRequest(
            $"{TitlePrefix} Updated", post.Slug, "New desc", "New body", null, 1, true, true, null);
        var updated = await (await _client.PutAsJsonAsync($"/api/blog/posts/{post.Id}", update, JsonOptions))
            .Content.ReadFromJsonAsync<BlogPostResponse>(JsonOptions);

        Assert.Equal(post.PublishedAt, updated!.PublishedAt);
        Assert.True(updated.IsFeatured);

        var unpublished = await (await _client.PutAsJsonAsync($"/api/blog/posts/{post.Id}", update with { IsPublished = false }, JsonOptions))
            .Content.ReadFromJsonAsync<BlogPostResponse>(JsonOptions);
        Assert.Null(unpublished!.PublishedAt);
    }

    [Fact]
    public async Task RecordView_IncrementsViewCount()
    {
        var post = await CreateAsync($"{TitlePrefix} Viewed");

        var response = await _anonymousClient.PostAsync($"/api/blog/published/{post.Slug}/views", null);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var reloaded = await _client.GetFromJsonAsync<BlogPostResponse>($"/api/blog/posts/{post.Id}", JsonOptions);
        Assert.Equal(1, reloaded!.PostViews);
    }

    [Fact]
    public async Task Delete_RemovesPost()
    {
        var post = await CreateAsync($"{TitlePrefix} Delete me");

        var response = await _client.DeleteAsync($"/api/blog/posts/{post.Id}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var getResponse = await _client.GetAsync($"/api/blog/posts/{post.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }
}
