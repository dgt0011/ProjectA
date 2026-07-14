using System.Net;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ProjectA.Api.Data;
using Xunit;

namespace ProjectA.Api.Tests.Features.BookmarkTypes.DeleteBookmarkType;

[Collection(nameof(ApiCollection))]
public class DeleteBookmarkTypeEndpointTests : IAsyncLifetime
{
    private const string TitlePrefix = "Delete Test BookmarkType";

    private readonly HttpClient _client;
    private readonly IDbConnectionFactory _connectionFactory;

    public DeleteBookmarkTypeEndpointTests(ApiFactory factory)
    {
        _client = factory.CreateAuthenticatedClient();
        _connectionFactory = factory.Services.GetRequiredService<IDbConnectionFactory>();
    }

    public async Task InitializeAsync() => await CleanUpAsync();

    public async Task DisposeAsync() => await CleanUpAsync();

    private async Task CleanUpAsync()
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();

        // Clear any blocking bookmarks first - otherwise the FK constraint this cleanup exists
        // to work around would stop the bookmark_types rows themselves being removed.
        await connection.ExecuteAsync(
            "UPDATE bookmarks SET bookmark_type_id = NULL WHERE bookmark_type_id IN " +
            "(SELECT id FROM bookmark_types WHERE title LIKE @Pattern);",
            new { Pattern = $"{TitlePrefix}%" });
        await connection.ExecuteAsync(
            "DELETE FROM bookmarks WHERE title LIKE @Pattern;",
            new { Pattern = $"{TitlePrefix}%" });
        await connection.ExecuteAsync(
            "DELETE FROM bookmark_types WHERE title LIKE @Pattern;",
            new { Pattern = $"{TitlePrefix}%" });
    }

    private async Task<long> SeedBookmarkTypeAsync(string suffix)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        return await connection.QuerySingleAsync<long>(
            "INSERT INTO bookmark_types (title) VALUES (@Title) RETURNING id;",
            new { Title = $"{TitlePrefix} {suffix}" });
    }

    [Fact]
    public async Task Delete_WhenExists_RemovesBookmarkTypeAndReturnsNoContent()
    {
        var id = await SeedBookmarkTypeAsync("ToDelete");

        var response = await _client.DeleteAsync($"/api/bookmarktypes/{id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var followUp = await _client.GetAsync($"/api/bookmarktypes/{id}");
        Assert.Equal(HttpStatusCode.NotFound, followUp.StatusCode);
    }

    [Fact]
    public async Task Delete_WhenMissing_ReturnsProblemDetails()
    {
        var response = await _client.DeleteAsync("/api/bookmarktypes/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Delete_WhenAssignedToBookmark_ReturnsConflict()
    {
        var id = await SeedBookmarkTypeAsync("InUse");

        using (var connection = await _connectionFactory.CreateConnectionAsync())
        {
            await connection.ExecuteAsync(
                "INSERT INTO bookmarks (url, title, bookmark_type_id) VALUES ('https://example.com/x', @Title, @BookmarkTypeId);",
                new { Title = $"{TitlePrefix} Blocking Bookmark", BookmarkTypeId = id });
        }

        var response = await _client.DeleteAsync($"/api/bookmarktypes/{id}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}
