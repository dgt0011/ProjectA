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

    private async Task<long> SeedBookmarkAsync()
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        return await connection.QuerySingleAsync<long>(
            "INSERT INTO bookmarks (url, title, description, rating) " +
            "VALUES ('https://example.com/original', @Title, 'Original description', 3) RETURNING id;",
            new { Title = $"{TitlePrefix} Original" });
    }

    [Fact]
    public async Task Put_WithValidRequest_UpdatesAndReturnsOk()
    {
        var id = await SeedBookmarkAsync();
        var request = new UpdateBookmarkEndpoint.UpdateBookmarkRequest(
            "https://example.com/updated", $"{TitlePrefix} Updated", "Updated description", 9);

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
            "https://example.com/updated", $"{TitlePrefix} Updated", null, null);

        var response = await _client.PutAsJsonAsync($"/api/bookmarks/{id}", request, JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await response.Content.ReadFromJsonAsync<UpdateBookmarkEndpoint.BookmarkResponse>(JsonOptions);
        Assert.NotNull(updated);
        Assert.Equal(3, updated.Rating);
    }

    [Fact]
    public async Task Put_WhenIdDoesNotExist_ReturnsProblemDetails()
    {
        var request = new UpdateBookmarkEndpoint.UpdateBookmarkRequest("https://example.com/missing", null, null, null);

        var response = await _client.PutAsJsonAsync("/api/bookmarks/999999", request, JsonOptions);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Put_WithMissingUrl_ReturnsValidationProblem()
    {
        var id = await SeedBookmarkAsync();
        var request = new UpdateBookmarkEndpoint.UpdateBookmarkRequest(" ", null, null, null);

        var response = await _client.PutAsJsonAsync($"/api/bookmarks/{id}", request, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
