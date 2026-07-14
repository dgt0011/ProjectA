using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ProjectA.Api.Data;
using ProjectA.Api.Features.Bookmarks.UpdateBookmark;
using Xunit;

namespace ProjectA.Api.Tests.Features.Bookmarks.UpdateBookmark;

[Collection(nameof(ApiCollection))]
public class UpdateBookmarkEndpointTests : IAsyncLifetime
{
    private const string TitlePrefix = "Update Test Bookmark";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _client;
    private readonly IDbConnectionFactory _connectionFactory;

    public UpdateBookmarkEndpointTests(ApiFactory factory)
    {
        _client = factory.CreateAuthenticatedClient();
        _connectionFactory = factory.Services.GetRequiredService<IDbConnectionFactory>();
    }

    public async Task InitializeAsync() => await CleanUpAsync();

    public async Task DisposeAsync() => await CleanUpAsync();

    private async Task CleanUpAsync()
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();

        await connection.ExecuteAsync(
            "DELETE FROM bookmark_categories WHERE " +
            "bookmark_id IN (SELECT id FROM bookmarks WHERE title LIKE @Pattern) OR " +
            "category_id IN (SELECT id FROM categories WHERE title LIKE @Pattern);",
            new { Pattern = $"{TitlePrefix}%" });
        await connection.ExecuteAsync("DELETE FROM bookmarks WHERE title LIKE @Pattern;", new { Pattern = $"{TitlePrefix}%" });
        await connection.ExecuteAsync("DELETE FROM categories WHERE title LIKE @Pattern;", new { Pattern = $"{TitlePrefix}%" });
        // Trailing space in the pattern matters here: "Update Test Bookmark%" would also
        // match "Update Test BookmarkType ..." rows seeded by the BookmarkTypes tests (since
        // "Bookmark" is a literal string-prefix of "BookmarkType"), which could still be
        // referenced by that other test's bookmark and trip the FK constraint on delete.
        await connection.ExecuteAsync("DELETE FROM bookmark_types WHERE title LIKE @Pattern;", new { Pattern = $"{TitlePrefix} %" });
    }

    private async Task<long> SeedBookmarkTypeAsync(string suffix)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        return await connection.QuerySingleAsync<long>(
            "INSERT INTO bookmark_types (title) VALUES (@Title) RETURNING id;",
            new { Title = $"{TitlePrefix} {suffix}" });
    }

    private async Task<long> SeedBookmarkAsync()
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        return await connection.QuerySingleAsync<long>(
            "INSERT INTO bookmarks (url, title, description, rating) " +
            "VALUES ('https://example.com/original', @Title, 'Original description', 3) RETURNING id;",
            new { Title = $"{TitlePrefix} Original" });
    }

    private async Task<long> SeedCategoryAsync(string suffix)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        return await connection.QuerySingleAsync<long>(
            "INSERT INTO categories (title) VALUES (@Title) RETURNING id;",
            new { Title = $"{TitlePrefix} {suffix}" });
    }

    private async Task LinkCategoryAsync(long bookmarkId, long categoryId)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        await connection.ExecuteAsync(
            "INSERT INTO bookmark_categories (bookmark_id, category_id) VALUES (@BookmarkId, @CategoryId);",
            new { BookmarkId = bookmarkId, CategoryId = categoryId });
    }

    [Fact]
    public async Task Put_WithValidRequest_UpdatesAndReturnsOk()
    {
        var id = await SeedBookmarkAsync();
        var request = new UpdateBookmarkEndpoint.UpdateBookmarkRequest(
            "https://example.com/updated", $"{TitlePrefix} Updated", "Updated description", 9, null, null);

        var response = await _client.PutAsJsonAsync($"/api/bookmarks/{id}", request, JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await response.Content.ReadFromJsonAsync<UpdateBookmarkEndpoint.BookmarkResponse>(JsonOptions);
        Assert.NotNull(updated);
        Assert.Equal(id, updated.Id);
        Assert.Equal("https://example.com/updated", updated.Url);
        Assert.Equal(9, updated.Rating);
        Assert.NotNull(updated.DateModified);
    }

    [Fact]
    public async Task Put_WithoutRating_KeepsExistingRating()
    {
        var id = await SeedBookmarkAsync();
        var request = new UpdateBookmarkEndpoint.UpdateBookmarkRequest(
            "https://example.com/updated", $"{TitlePrefix} Updated", null, null, null, null);

        var response = await _client.PutAsJsonAsync($"/api/bookmarks/{id}", request, JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await response.Content.ReadFromJsonAsync<UpdateBookmarkEndpoint.BookmarkResponse>(JsonOptions);
        Assert.NotNull(updated);
        Assert.Equal(3, updated.Rating);
    }

    [Fact]
    public async Task Put_WhenIdDoesNotExist_ReturnsProblemDetails()
    {
        var request = new UpdateBookmarkEndpoint.UpdateBookmarkRequest("https://example.com/missing", null, null, null, null, null);

        var response = await _client.PutAsJsonAsync("/api/bookmarks/999999", request, JsonOptions);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Put_WithMissingUrl_ReturnsValidationProblem()
    {
        var id = await SeedBookmarkAsync();
        var request = new UpdateBookmarkEndpoint.UpdateBookmarkRequest(" ", null, null, null, null, null);

        var response = await _client.PutAsJsonAsync($"/api/bookmarks/{id}", request, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Put_WithCategoryIds_ReplacesExistingAssociations()
    {
        var bookmarkId = await SeedBookmarkAsync();
        var oldCategoryId = await SeedCategoryAsync("Old");
        var newCategoryId = await SeedCategoryAsync("New");
        await LinkCategoryAsync(bookmarkId, oldCategoryId);

        var request = new UpdateBookmarkEndpoint.UpdateBookmarkRequest(
            "https://example.com/original", null, null, null, null, [newCategoryId]);

        var response = await _client.PutAsJsonAsync($"/api/bookmarks/{bookmarkId}", request, JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await response.Content.ReadFromJsonAsync<UpdateBookmarkEndpoint.BookmarkResponse>(JsonOptions);
        Assert.NotNull(updated);
        Assert.Equal([newCategoryId], updated.CategoryIds);
    }

    [Fact]
    public async Task Put_WithoutCategoryIds_LeavesExistingAssociationsUnchanged()
    {
        var bookmarkId = await SeedBookmarkAsync();
        var categoryId = await SeedCategoryAsync("Untouched");
        await LinkCategoryAsync(bookmarkId, categoryId);

        var request = new UpdateBookmarkEndpoint.UpdateBookmarkRequest(
            "https://example.com/original", null, null, null, null, null);

        var response = await _client.PutAsJsonAsync($"/api/bookmarks/{bookmarkId}", request, JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await response.Content.ReadFromJsonAsync<UpdateBookmarkEndpoint.BookmarkResponse>(JsonOptions);
        Assert.NotNull(updated);
        Assert.Equal([categoryId], updated.CategoryIds);
    }

    [Fact]
    public async Task Put_WithEmptyCategoryIds_ClearsExistingAssociations()
    {
        var bookmarkId = await SeedBookmarkAsync();
        var categoryId = await SeedCategoryAsync("ToRemove");
        await LinkCategoryAsync(bookmarkId, categoryId);

        var request = new UpdateBookmarkEndpoint.UpdateBookmarkRequest(
            "https://example.com/original", null, null, null, null, []);

        var response = await _client.PutAsJsonAsync($"/api/bookmarks/{bookmarkId}", request, JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await response.Content.ReadFromJsonAsync<UpdateBookmarkEndpoint.BookmarkResponse>(JsonOptions);
        Assert.NotNull(updated);
        Assert.Empty(updated.CategoryIds);
    }

    [Fact]
    public async Task Put_WithInvalidCategoryId_ReturnsValidationProblem_AndLeavesBookmarkUnchanged()
    {
        var bookmarkId = await SeedBookmarkAsync();
        var request = new UpdateBookmarkEndpoint.UpdateBookmarkRequest(
            "https://example.com/should-not-apply", null, null, null, null, [999999]);

        var response = await _client.PutAsJsonAsync($"/api/bookmarks/{bookmarkId}", request, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemResponse>(JsonOptions);
        Assert.NotNull(problem);
        Assert.True(problem.Errors.ContainsKey("CategoryIds"));

        // The rest of the update should have rolled back too, not just the category change.
        using var connection = await _connectionFactory.CreateConnectionAsync();
        var url = await connection.QuerySingleAsync<string>(
            "SELECT url FROM bookmarks WHERE id = @Id;", new { Id = bookmarkId });
        Assert.Equal("https://example.com/original", url);
    }

    [Fact]
    public async Task Put_WithBookmarkTypeId_SetsBookmarkType()
    {
        var bookmarkId = await SeedBookmarkAsync();
        var bookmarkTypeId = await SeedBookmarkTypeAsync("Github");

        var request = new UpdateBookmarkEndpoint.UpdateBookmarkRequest(
            "https://example.com/original", null, null, null, bookmarkTypeId, null);

        var response = await _client.PutAsJsonAsync($"/api/bookmarks/{bookmarkId}", request, JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await response.Content.ReadFromJsonAsync<UpdateBookmarkEndpoint.BookmarkResponse>(JsonOptions);
        Assert.NotNull(updated);
        Assert.Equal(bookmarkTypeId, updated.BookmarkTypeId);
    }

    [Fact]
    public async Task Put_WithNullBookmarkTypeId_ClearsExistingBookmarkType()
    {
        var bookmarkTypeId = await SeedBookmarkTypeAsync("ToClear");
        long bookmarkId;
        using (var connection = await _connectionFactory.CreateConnectionAsync())
        {
            bookmarkId = await connection.QuerySingleAsync<long>(
                "INSERT INTO bookmarks (url, title, bookmark_type_id) VALUES " +
                "('https://example.com/original', @Title, @BookmarkTypeId) RETURNING id;",
                new { Title = $"{TitlePrefix} Typed", BookmarkTypeId = bookmarkTypeId });
        }

        var request = new UpdateBookmarkEndpoint.UpdateBookmarkRequest(
            "https://example.com/original", null, null, null, null, null);

        var response = await _client.PutAsJsonAsync($"/api/bookmarks/{bookmarkId}", request, JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await response.Content.ReadFromJsonAsync<UpdateBookmarkEndpoint.BookmarkResponse>(JsonOptions);
        Assert.NotNull(updated);
        Assert.Null(updated.BookmarkTypeId);
    }

    [Fact]
    public async Task Put_WithInvalidBookmarkTypeId_ReturnsValidationProblem_AndLeavesBookmarkUnchanged()
    {
        var bookmarkId = await SeedBookmarkAsync();
        var request = new UpdateBookmarkEndpoint.UpdateBookmarkRequest(
            "https://example.com/should-not-apply", null, null, null, 999999, null);

        var response = await _client.PutAsJsonAsync($"/api/bookmarks/{bookmarkId}", request, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemResponse>(JsonOptions);
        Assert.NotNull(problem);
        Assert.True(problem.Errors.ContainsKey("BookmarkTypeId"));

        using var connection = await _connectionFactory.CreateConnectionAsync();
        var url = await connection.QuerySingleAsync<string>(
            "SELECT url FROM bookmarks WHERE id = @Id;", new { Id = bookmarkId });
        Assert.Equal("https://example.com/original", url);
    }

    private sealed record ValidationProblemResponse(
        string? Type,
        string? Title,
        int? Status,
        Dictionary<string, string[]> Errors);
}
