using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ProjectA.Api.Data;
using ProjectA.Api.Features.Bookmarks.CreateBookmark;
using ProjectA.Api.Features.Bookmarks.GetBookmarkById;
using Xunit;

namespace ProjectA.Api.Tests.Features.Bookmarks.CreateBookmark;

[Collection(nameof(ApiCollection))]
public class CreateBookmarkEndpointTests : IAsyncLifetime
{
    private const string TitlePrefix = "Create Test Bookmark";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _client;
    private readonly IDbConnectionFactory _connectionFactory;

    public CreateBookmarkEndpointTests(ApiFactory factory)
    {
        _client = factory.CreateAuthenticatedClient();
        _connectionFactory = factory.Services.GetRequiredService<IDbConnectionFactory>();
    }

    public async Task InitializeAsync() => await CleanUpAsync();

    public async Task DisposeAsync() => await CleanUpAsync();

    private async Task CleanUpAsync()
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();

        // Join rows first - the FK constraint would otherwise stop the bookmark/category
        // rows themselves being removed.
        await connection.ExecuteAsync(
            "DELETE FROM bookmark_categories WHERE " +
            "bookmark_id IN (SELECT id FROM bookmarks WHERE title LIKE @Pattern) OR " +
            "category_id IN (SELECT id FROM categories WHERE title LIKE @Pattern);",
            new { Pattern = $"{TitlePrefix}%" });
        await connection.ExecuteAsync("DELETE FROM bookmarks WHERE title LIKE @Pattern;", new { Pattern = $"{TitlePrefix}%" });
        await connection.ExecuteAsync("DELETE FROM categories WHERE title LIKE @Pattern;", new { Pattern = $"{TitlePrefix}%" });
        // Trailing space in the pattern matters here: "Create Test Bookmark%" would also
        // match "Create Test BookmarkType ..." rows seeded by the BookmarkTypes tests (since
        // "Bookmark" is a literal string-prefix of "BookmarkType"), which could still be
        // referenced by that other test's bookmark and trip the FK constraint on delete.
        await connection.ExecuteAsync("DELETE FROM bookmark_types WHERE title LIKE @Pattern;", new { Pattern = $"{TitlePrefix} %" });
    }

    private async Task<long> SeedCategoryAsync(string suffix)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        return await connection.QuerySingleAsync<long>(
            "INSERT INTO categories (title) VALUES (@Title) RETURNING id;",
            new { Title = $"{TitlePrefix} {suffix}" });
    }

    private async Task<long> SeedBookmarkTypeAsync(string suffix)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        return await connection.QuerySingleAsync<long>(
            "INSERT INTO bookmark_types (title) VALUES (@Title) RETURNING id;",
            new { Title = $"{TitlePrefix} {suffix}" });
    }

    [Fact]
    public async Task Post_WithValidRequest_CreatesBookmarkAndReturnsCreated()
    {
        var request = new CreateBookmarkEndpoint.CreateBookmarkRequest(
            "https://example.com/new", $"{TitlePrefix} New", "A description", 8, null, null);

        var response = await _client.PostAsJsonAsync("/api/bookmarks", request, JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var created = await response.Content.ReadFromJsonAsync<CreateBookmarkEndpoint.BookmarkResponse>(JsonOptions);
        Assert.NotNull(created);
        Assert.True(created.Id > 0);
        Assert.Contains($"/api/bookmarks/{created.Id}", response.Headers.Location!.ToString());
        Assert.Equal("https://example.com/new", created.Url);
        Assert.Equal(8, created.Rating);
        Assert.Empty(created.CategoryIds);
    }

    [Fact]
    public async Task Post_WithoutRating_DefaultsToOne()
    {
        var request = new CreateBookmarkEndpoint.CreateBookmarkRequest(
            "https://example.com/default-rating", $"{TitlePrefix} DefaultRating", null, null, null, null);

        var response = await _client.PostAsJsonAsync("/api/bookmarks", request, JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<CreateBookmarkEndpoint.BookmarkResponse>(JsonOptions);
        Assert.NotNull(created);
        Assert.Equal(1, created.Rating);
    }

    [Fact]
    public async Task Post_WithMissingUrl_ReturnsValidationProblem()
    {
        var request = new CreateBookmarkEndpoint.CreateBookmarkRequest(" ", null, null, null, null, null);

        var response = await _client.PostAsJsonAsync("/api/bookmarks", request, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemResponse>(JsonOptions);
        Assert.NotNull(problem);
        Assert.True(problem.Errors.ContainsKey("Url"));
    }

    [Fact]
    public async Task Post_WithOutOfRangeRating_ReturnsValidationProblem()
    {
        var request = new CreateBookmarkEndpoint.CreateBookmarkRequest(
            "https://example.com/bad-rating", $"{TitlePrefix} BadRating", null, 11, null, null);

        var response = await _client.PostAsJsonAsync("/api/bookmarks", request, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemResponse>(JsonOptions);
        Assert.NotNull(problem);
        Assert.True(problem.Errors.ContainsKey("Rating"));
    }

    [Fact]
    public async Task Post_WithCategoryIds_AssociatesCategoriesAndReturnsThem()
    {
        var categoryOneId = await SeedCategoryAsync("One");
        var categoryTwoId = await SeedCategoryAsync("Two");

        var request = new CreateBookmarkEndpoint.CreateBookmarkRequest(
            "https://example.com/categorized", $"{TitlePrefix} Categorized", null, null, null,
            [categoryOneId, categoryTwoId]);

        var response = await _client.PostAsJsonAsync("/api/bookmarks", request, JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<CreateBookmarkEndpoint.BookmarkResponse>(JsonOptions);
        Assert.NotNull(created);
        Assert.Equal(
            new[] { categoryOneId, categoryTwoId }.OrderBy(x => x),
            created.CategoryIds.OrderBy(x => x));

        // Confirm the GetById response independently reflects the same associations.
        var getResponse = await _client.GetAsync($"/api/bookmarks/{created.Id}");
        var fetched = await getResponse.Content
            .ReadFromJsonAsync<GetBookmarkByIdEndpoint.BookmarkResponse>(JsonOptions);
        Assert.NotNull(fetched);
        Assert.Equal(
            new[] { categoryOneId, categoryTwoId }.OrderBy(x => x),
            fetched.CategoryIds.OrderBy(x => x));
    }

    [Fact]
    public async Task Post_WithInvalidCategoryId_ReturnsValidationProblem_AndCreatesNoBookmark()
    {
        var request = new CreateBookmarkEndpoint.CreateBookmarkRequest(
            "https://example.com/bad-category", $"{TitlePrefix} BadCategory", null, null, null, [999999]);

        var response = await _client.PostAsJsonAsync("/api/bookmarks", request, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemResponse>(JsonOptions);
        Assert.NotNull(problem);
        Assert.True(problem.Errors.ContainsKey("CategoryIds"));

        // The whole create should have rolled back - no orphaned bookmark left behind.
        using var connection = await _connectionFactory.CreateConnectionAsync();
        var count = await connection.QuerySingleAsync<long>(
            "SELECT COUNT(*) FROM bookmarks WHERE title = @Title;",
            new { Title = $"{TitlePrefix} BadCategory" });
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task Post_WithBookmarkTypeId_AssociatesBookmarkTypeAndReturnsIt()
    {
        var bookmarkTypeId = await SeedBookmarkTypeAsync("Github");

        var request = new CreateBookmarkEndpoint.CreateBookmarkRequest(
            "https://example.com/typed", $"{TitlePrefix} Typed", null, null, bookmarkTypeId, null);

        var response = await _client.PostAsJsonAsync("/api/bookmarks", request, JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<CreateBookmarkEndpoint.BookmarkResponse>(JsonOptions);
        Assert.NotNull(created);
        Assert.Equal(bookmarkTypeId, created.BookmarkTypeId);

        var getResponse = await _client.GetAsync($"/api/bookmarks/{created.Id}");
        var fetched = await getResponse.Content
            .ReadFromJsonAsync<GetBookmarkByIdEndpoint.BookmarkResponse>(JsonOptions);
        Assert.NotNull(fetched);
        Assert.Equal(bookmarkTypeId, fetched.BookmarkTypeId);
    }

    [Fact]
    public async Task Post_WithInvalidBookmarkTypeId_ReturnsValidationProblem_AndCreatesNoBookmark()
    {
        var request = new CreateBookmarkEndpoint.CreateBookmarkRequest(
            "https://example.com/bad-bookmark-type", $"{TitlePrefix} BadBookmarkType", null, null, 999999, null);

        var response = await _client.PostAsJsonAsync("/api/bookmarks", request, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemResponse>(JsonOptions);
        Assert.NotNull(problem);
        Assert.True(problem.Errors.ContainsKey("BookmarkTypeId"));

        using var connection = await _connectionFactory.CreateConnectionAsync();
        var count = await connection.QuerySingleAsync<long>(
            "SELECT COUNT(*) FROM bookmarks WHERE title = @Title;",
            new { Title = $"{TitlePrefix} BadBookmarkType" });
        Assert.Equal(0, count);
    }

    private sealed record ValidationProblemResponse(
        string? Type,
        string? Title,
        int? Status,
        Dictionary<string, string[]> Errors);
}
