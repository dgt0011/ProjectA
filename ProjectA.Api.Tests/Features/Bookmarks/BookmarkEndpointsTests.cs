using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ProjectA.Api.Data;
using ProjectA.Api.Features.Bookmarks.GetBookmarkById;
using ProjectA.Api.Features.Bookmarks.GetBookmarkList;
using Xunit;

namespace ProjectA.Api.Tests.Features.Bookmarks;

[Collection(nameof(ApiCollection))]
public class BookmarkEndpointsTests : IAsyncLifetime
{
    private const string TitlePrefix = "List Test Bookmark";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _client;
    private readonly IDbConnectionFactory _connectionFactory;

    public BookmarkEndpointsTests(ApiFactory factory)
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
        await connection.ExecuteAsync(
            "DELETE FROM bookmarks WHERE title LIKE @Pattern;",
            new { Pattern = $"{TitlePrefix}%" });
        await connection.ExecuteAsync(
            "DELETE FROM categories WHERE title LIKE @Pattern;",
            new { Pattern = $"{TitlePrefix}%" });
        // Trailing space in the pattern matters here: "List Test Bookmark%" would also match
        // "List Test BookmarkType ..." rows seeded by the BookmarkTypes tests (since
        // "Bookmark" is a literal string-prefix of "BookmarkType"), which could still be
        // referenced by that other test's bookmark and trip the FK constraint on delete.
        await connection.ExecuteAsync(
            "DELETE FROM bookmark_types WHERE title LIKE @Pattern;",
            new { Pattern = $"{TitlePrefix} %" });
    }

    [Fact]
    public async Task GetList_ReturnsAllBookmarks()
    {
        using (var connection = await _connectionFactory.CreateConnectionAsync())
        {
            await connection.ExecuteAsync(
                "INSERT INTO bookmarks (url, title, description, rating) VALUES " +
                "('https://example.com/a', @TitleA, 'Description A', 5), " +
                "('https://example.com/b', @TitleB, NULL, 1);",
                new { TitleA = $"{TitlePrefix} A", TitleB = $"{TitlePrefix} B" });
        }

        var response = await _client.GetAsync("/api/bookmarks");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var bookmarks = await response.Content
            .ReadFromJsonAsync<List<GetBookmarkListEndpoint.BookmarkListItemResponse>>(JsonOptions);
        Assert.NotNull(bookmarks);

        var seeded = bookmarks.Where(b => b.Title != null && b.Title.StartsWith(TitlePrefix)).ToList();
        Assert.Equal(2, seeded.Count);
        Assert.Contains(seeded, b => b.Title == $"{TitlePrefix} A" && b.Rating == 5);
        Assert.Contains(seeded, b => b.Title == $"{TitlePrefix} B" && b.Rating == 1);
        Assert.All(seeded, b => Assert.Empty(b.CategoryIds));
        Assert.All(seeded, b => Assert.Null(b.BookmarkTypeId));
    }

    [Fact]
    public async Task GetList_IncludesBookmarkTypeId()
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();

        var bookmarkTypeId = await connection.QuerySingleAsync<long>(
            "INSERT INTO bookmark_types (title) VALUES (@Title) RETURNING id;",
            new { Title = $"{TitlePrefix} ListType" });
        var bookmarkId = await connection.QuerySingleAsync<long>(
            "INSERT INTO bookmarks (url, title, bookmark_type_id) VALUES " +
            "('https://example.com/list-typed', @Title, @BookmarkTypeId) RETURNING id;",
            new { Title = $"{TitlePrefix} ListTyped", BookmarkTypeId = bookmarkTypeId });

        var response = await _client.GetAsync("/api/bookmarks");
        var bookmarks = await response.Content
            .ReadFromJsonAsync<List<GetBookmarkListEndpoint.BookmarkListItemResponse>>(JsonOptions);
        Assert.NotNull(bookmarks);

        var found = bookmarks.Single(b => b.Id == bookmarkId);
        Assert.Equal(bookmarkTypeId, found.BookmarkTypeId);
    }

    [Fact]
    public async Task GetList_IncludesCategoryAssociations()
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();

        var bookmarkId = await connection.QuerySingleAsync<long>(
            "INSERT INTO bookmarks (url, title) VALUES ('https://example.com/list-categorized', @Title) RETURNING id;",
            new { Title = $"{TitlePrefix} ListCategorized" });
        var categoryId = await connection.QuerySingleAsync<long>(
            "INSERT INTO categories (title) VALUES (@Title) RETURNING id;",
            new { Title = $"{TitlePrefix} ListCategory" });
        await connection.ExecuteAsync(
            "INSERT INTO bookmark_categories (bookmark_id, category_id) VALUES (@BookmarkId, @CategoryId);",
            new { BookmarkId = bookmarkId, CategoryId = categoryId });

        var response = await _client.GetAsync("/api/bookmarks");
        var bookmarks = await response.Content
            .ReadFromJsonAsync<List<GetBookmarkListEndpoint.BookmarkListItemResponse>>(JsonOptions);
        Assert.NotNull(bookmarks);

        var found = bookmarks.Single(b => b.Id == bookmarkId);
        Assert.Equal([categoryId], found.CategoryIds);
    }

    [Fact]
    public async Task GetById_WhenExists_ReturnsBookmark()
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        var id = await connection.QuerySingleAsync<long>(
            "INSERT INTO bookmarks (url, title, rating) VALUES ('https://example.com/byid', @Title, 7) RETURNING id;",
            new { Title = $"{TitlePrefix} ById" });

        var response = await _client.GetAsync($"/api/bookmarks/{id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var bookmark = await response.Content
            .ReadFromJsonAsync<GetBookmarkByIdEndpoint.BookmarkResponse>(JsonOptions);
        Assert.NotNull(bookmark);
        Assert.Equal(id, bookmark.Id);
        Assert.Equal("https://example.com/byid", bookmark.Url);
        Assert.Equal(7, bookmark.Rating);
        Assert.Empty(bookmark.CategoryIds);
        Assert.Null(bookmark.BookmarkTypeId);
    }

    [Fact]
    public async Task GetById_IncludesBookmarkTypeId()
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();

        var bookmarkTypeId = await connection.QuerySingleAsync<long>(
            "INSERT INTO bookmark_types (title) VALUES (@Title) RETURNING id;",
            new { Title = $"{TitlePrefix} ByIdType" });
        var bookmarkId = await connection.QuerySingleAsync<long>(
            "INSERT INTO bookmarks (url, title, bookmark_type_id) VALUES " +
            "('https://example.com/byid-typed', @Title, @BookmarkTypeId) RETURNING id;",
            new { Title = $"{TitlePrefix} ByIdTyped", BookmarkTypeId = bookmarkTypeId });

        var response = await _client.GetAsync($"/api/bookmarks/{bookmarkId}");
        var bookmark = await response.Content
            .ReadFromJsonAsync<GetBookmarkByIdEndpoint.BookmarkResponse>(JsonOptions);
        Assert.NotNull(bookmark);
        Assert.Equal(bookmarkTypeId, bookmark.BookmarkTypeId);
    }

    [Fact]
    public async Task GetById_IncludesCategoryAssociations()
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();

        var bookmarkId = await connection.QuerySingleAsync<long>(
            "INSERT INTO bookmarks (url, title) VALUES ('https://example.com/byid-categorized', @Title) RETURNING id;",
            new { Title = $"{TitlePrefix} ByIdCategorized" });
        var categoryId = await connection.QuerySingleAsync<long>(
            "INSERT INTO categories (title) VALUES (@Title) RETURNING id;",
            new { Title = $"{TitlePrefix} ByIdCategory" });
        await connection.ExecuteAsync(
            "INSERT INTO bookmark_categories (bookmark_id, category_id) VALUES (@BookmarkId, @CategoryId);",
            new { BookmarkId = bookmarkId, CategoryId = categoryId });

        var response = await _client.GetAsync($"/api/bookmarks/{bookmarkId}");
        var bookmark = await response.Content
            .ReadFromJsonAsync<GetBookmarkByIdEndpoint.BookmarkResponse>(JsonOptions);
        Assert.NotNull(bookmark);
        Assert.Equal([categoryId], bookmark.CategoryIds);
    }

    [Fact]
    public async Task GetById_WhenMissing_ReturnsProblemDetails()
    {
        var response = await _client.GetAsync("/api/bookmarks/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}
