using System.Net;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ProjectA.Api.Data;
using Xunit;

namespace ProjectA.Api.Tests.Features.Bookmarks.DeleteBookmark;

[Collection(nameof(ApiCollection))]
public class DeleteBookmarkEndpointTests : IAsyncLifetime
{
    private const string TitlePrefix = "Delete Test Bookmark";

    private readonly HttpClient _client;
    private readonly IDbConnectionFactory _connectionFactory;

    public DeleteBookmarkEndpointTests(ApiFactory factory)
    {
        _client = factory.CreateClient();
        _connectionFactory = factory.Services.GetRequiredService<IDbConnectionFactory>();
    }

    public async Task InitializeAsync() => await CleanUpAsync();

    public async Task DisposeAsync() => await CleanUpAsync();

    private async Task CleanUpAsync()
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();

        // Delete the join rows first - otherwise the FK constraint this cleanup exists to
        // work around would stop the bookmark/note rows themselves being removed.
        await connection.ExecuteAsync(
            "DELETE FROM note_bookmarks WHERE " +
            "bookmark_id IN (SELECT id FROM bookmarks WHERE title LIKE @Pattern) OR " +
            "note_id IN (SELECT id FROM notes WHERE title LIKE @Pattern);",
            new { Pattern = $"{TitlePrefix}%" });
        await connection.ExecuteAsync("DELETE FROM notes WHERE title LIKE @Pattern;", new { Pattern = $"{TitlePrefix}%" });
        await connection.ExecuteAsync("DELETE FROM bookmarks WHERE title LIKE @Pattern;", new { Pattern = $"{TitlePrefix}%" });
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
}
