using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ProjectA.Api.Data;
using ProjectA.Api.Features.Bookmarks.CreateBookmark;
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
    public async Task Post_WithValidRequest_CreatesBookmarkAndReturnsCreated()
    {
        var request = new CreateBookmarkEndpoint.CreateBookmarkRequest(
            "https://example.com/new", $"{TitlePrefix} New", "A description", 8);

        var response = await _client.PostAsJsonAsync("/api/bookmarks", request, JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var created = await response.Content.ReadFromJsonAsync<CreateBookmarkEndpoint.BookmarkResponse>(JsonOptions);
        Assert.NotNull(created);
        Assert.True(created.Id > 0);
        Assert.Contains($"/api/bookmarks/{created.Id}", response.Headers.Location!.ToString());
        Assert.Equal("https://example.com/new", created.Url);
        Assert.Equal(8, created.Rating);
    }

    [Fact]
    public async Task Post_WithoutRating_DefaultsToOne()
    {
        var request = new CreateBookmarkEndpoint.CreateBookmarkRequest(
            "https://example.com/default-rating", $"{TitlePrefix} DefaultRating", null, null);

        var response = await _client.PostAsJsonAsync("/api/bookmarks", request, JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<CreateBookmarkEndpoint.BookmarkResponse>(JsonOptions);
        Assert.NotNull(created);
        Assert.Equal(1, created.Rating);
    }

    [Fact]
    public async Task Post_WithMissingUrl_ReturnsValidationProblem()
    {
        var request = new CreateBookmarkEndpoint.CreateBookmarkRequest(" ", null, null, null);

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
            "https://example.com/bad-rating", $"{TitlePrefix} BadRating", null, 11);

        var response = await _client.PostAsJsonAsync("/api/bookmarks", request, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemResponse>(JsonOptions);
        Assert.NotNull(problem);
        Assert.True(problem.Errors.ContainsKey("Rating"));
    }

    private sealed record ValidationProblemResponse(
        string? Type,
        string? Title,
        int? Status,
        Dictionary<string, string[]> Errors);
}
