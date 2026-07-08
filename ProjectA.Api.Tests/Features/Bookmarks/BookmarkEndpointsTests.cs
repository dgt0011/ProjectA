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
        _client = factory.CreateClient();
        _connectionFactory = factory.Services.GetRequiredService<IDbConnectionFactory>();
    }

    public async Task InitializeAsync() => await CleanUpAsync();

    public async Task DisposeAsync() => await CleanUpAsync();

    private async Task CleanUpAsync()
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        await connection.ExecuteAsync(
            "DELETE FROM bookmarks WHERE title LIKE @Pattern;",
            new { Pattern = $"{TitlePrefix}%" });
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
    }

    [Fact]
    public async Task GetById_WhenMissing_ReturnsProblemDetails()
    {
        var response = await _client.GetAsync("/api/bookmarks/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}
