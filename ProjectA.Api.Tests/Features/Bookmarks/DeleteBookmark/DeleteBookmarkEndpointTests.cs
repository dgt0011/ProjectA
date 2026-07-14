using System.Net;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ProjectA.Api.Data;
using Xunit;
using Xunit.Abstractions;

namespace ProjectA.Api.Tests.Features.Bookmarks.DeleteBookmark;

[Collection(nameof(ApiCollection))]
public class DeleteBookmarkEndpointTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _testOutputHelper;
    private const string TitlePrefix = "Delete Test Bookmark";

    private readonly HttpClient _client;
    private readonly IDbConnectionFactory _connectionFactory;

    public DeleteBookmarkEndpointTests(ApiFactory factory, ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
        _client = factory.CreateAuthenticatedClient();
        _connectionFactory = factory.Services.GetRequiredService<IDbConnectionFactory>();
    }

    public async Task InitializeAsync() => await CleanUpAsync();

    public async Task DisposeAsync() => await CleanUpAsync();

    private async Task CleanUpAsync()
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();

        // Delete the join rows first - otherwise the FK constraint this cleanup exists to
        // work around would stop the bookmark/note/category rows themselves being removed.
        await connection.ExecuteAsync(
            "DELETE FROM note_bookmarks WHERE " +
            "bookmark_id IN (SELECT id FROM bookmarks WHERE title LIKE @Pattern) OR " +
            "note_id IN (SELECT id FROM notes WHERE title LIKE @Pattern);",
            new { Pattern = $"{TitlePrefix}%" });
        await connection.ExecuteAsync(
            "DELETE FROM bookmark_categories WHERE " +
            "bookmark_id IN (SELECT id FROM bookmarks WHERE title LIKE @Pattern) OR " +
            "category_id IN (SELECT id FROM categories WHERE title LIKE @Pattern);",
            new { Pattern = $"{TitlePrefix}%" });
        await connection.ExecuteAsync("DELETE FROM notes WHERE title LIKE @Pattern;", new { Pattern = $"{TitlePrefix}%" });
        await connection.ExecuteAsync("DELETE FROM bookmarks WHERE title LIKE @Pattern;", new { Pattern = $"{TitlePrefix}%" });
        await connection.ExecuteAsync("DELETE FROM categories WHERE title LIKE @Pattern;", new { Pattern = $"{TitlePrefix}%" });
    }

    private async Task<long> SeedBookmarkAsync(string suffix)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        return await connection.QuerySingleAsync<long>(
            "INSERT INTO bookmarks (url, title) VALUES ('https://example.com/x', @Title) RETURNING id;",
            new { Title = $"{TitlePrefix} {suffix}" });
    }

    [Fact]
    public async Task Delete_WhenExists_RemovesBookmarkAndReturnsNoContent()
    {
        var id = await SeedBookmarkAsync("ToDelete");

        var response = await _client.DeleteAsync($"/api/bookmarks/{id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var followUp = await _client.GetAsync($"/api/bookmarks/{id}");
        Assert.Equal(HttpStatusCode.NotFound, followUp.StatusCode);
    }

    [Fact]
    public async Task Delete_WhenMissing_ReturnsProblemDetails()
    {
        var response = await _client.DeleteAsync("/api/bookmarks/999999");

        //debuggery
        _testOutputHelper.WriteLine(response.Headers.WwwAuthenticate.ToString());
        
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Delete_WhenReferencedByNote_ReturnsConflict()
    {
        var bookmarkId = await SeedBookmarkAsync("InUse");

        using (var connection = await _connectionFactory.CreateConnectionAsync())
        {
            var noteId = await connection.QuerySingleAsync<long>(
                "INSERT INTO notes (title) VALUES (@Title) RETURNING id;",
                new { Title = $"{TitlePrefix} Linking Note" });

            await connection.ExecuteAsync(
                "INSERT INTO note_bookmarks (note_id, bookmark_id) VALUES (@NoteId, @BookmarkId);",
                new { NoteId = noteId, BookmarkId = bookmarkId });
        }

        var response = await _client.DeleteAsync($"/api/bookmarks/{bookmarkId}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Delete_WhenHasCategoryAssociation_SucceedsAndLeavesCategoryIntact()
    {
        var bookmarkId = await SeedBookmarkAsync("Categorized");

        long categoryId;
        using (var connection = await _connectionFactory.CreateConnectionAsync())
        {
            categoryId = await connection.QuerySingleAsync<long>(
                "INSERT INTO categories (title) VALUES (@Title) RETURNING id;",
                new { Title = $"{TitlePrefix} Category" });

            await connection.ExecuteAsync(
                "INSERT INTO bookmark_categories (bookmark_id, category_id) VALUES (@BookmarkId, @CategoryId);",
                new { BookmarkId = bookmarkId, CategoryId = categoryId });
        }

        // The category association must not block deletion...
        var response = await _client.DeleteAsync($"/api/bookmarks/{bookmarkId}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // ...and the category itself must survive, untouched.
        var categoryResponse = await _client.GetAsync($"/api/categories/{categoryId}");
        Assert.Equal(HttpStatusCode.OK, categoryResponse.StatusCode);

        // The join row should be gone too, not left dangling.
        using var verifyConnection = await _connectionFactory.CreateConnectionAsync();
        var remainingLinks = await verifyConnection.QuerySingleAsync<long>(
            "SELECT COUNT(*) FROM bookmark_categories WHERE bookmark_id = @BookmarkId;",
            new { BookmarkId = bookmarkId });
        Assert.Equal(0, remainingLinks);
    }
}
