using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ProjectA.Api.Data;
using ProjectA.Api.Features.BookmarkTypes.CreateBookmarkType;
using Xunit;

namespace ProjectA.Api.Tests.Features.BookmarkTypes.CreateBookmarkType;

[Collection(nameof(ApiCollection))]
public class CreateBookmarkTypeEndpointTests : IAsyncLifetime
{
    private const string TitlePrefix = "Create Test BookmarkType";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _client;
    private readonly IDbConnectionFactory _connectionFactory;

    public CreateBookmarkTypeEndpointTests(ApiFactory factory)
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
            "DELETE FROM bookmark_types WHERE title LIKE @Pattern;",
            new { Pattern = $"{TitlePrefix}%" });
    }

    [Fact]
    public async Task Post_WithValidRequest_CreatesBookmarkTypeAndReturnsCreated()
    {
        var request = new CreateBookmarkTypeEndpoint.CreateBookmarkTypeRequest(
            $"{TitlePrefix} New", "#1A2B3C", null, null);

        var response = await _client.PostAsJsonAsync("/api/bookmarktypes", request, JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var created = await response.Content.ReadFromJsonAsync<CreateBookmarkTypeEndpoint.BookmarkTypeResponse>(JsonOptions);
        Assert.NotNull(created);
        Assert.True(created.Id > 0);
        Assert.Contains($"/api/bookmarktypes/{created.Id}", response.Headers.Location!.ToString());
        Assert.Equal($"{TitlePrefix} New", created.Title);
        Assert.Equal("#1A2B3C", created.Color);
        Assert.False(created.HasIcon);
    }

    [Fact]
    public async Task Post_WithIcon_CreatesBookmarkTypeAndStoresIcon()
    {
        var iconBytes = new byte[] { 1, 2, 3, 4 };
        var request = new CreateBookmarkTypeEndpoint.CreateBookmarkTypeRequest(
            $"{TitlePrefix} WithIcon", null, Convert.ToBase64String(iconBytes), "image/png");

        var response = await _client.PostAsJsonAsync("/api/bookmarktypes", request, JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<CreateBookmarkTypeEndpoint.BookmarkTypeResponse>(JsonOptions);
        Assert.NotNull(created);
        Assert.True(created.HasIcon);

        var iconResponse = await _client.GetAsync($"/api/bookmarktypes/{created.Id}/icon");
        Assert.Equal(HttpStatusCode.OK, iconResponse.StatusCode);
        Assert.Equal("image/png", iconResponse.Content.Headers.ContentType?.MediaType);
        Assert.Equal(iconBytes, await iconResponse.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Post_WithMissingTitle_ReturnsValidationProblem()
    {
        var request = new CreateBookmarkTypeEndpoint.CreateBookmarkTypeRequest(" ", null, null, null);

        var response = await _client.PostAsJsonAsync("/api/bookmarktypes", request, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemResponse>(JsonOptions);
        Assert.NotNull(problem);
        Assert.True(problem.Errors.ContainsKey("Title"));
    }

    [Fact]
    public async Task Post_WithInvalidColor_ReturnsValidationProblem()
    {
        var request = new CreateBookmarkTypeEndpoint.CreateBookmarkTypeRequest(
            $"{TitlePrefix} BadColor", "not-a-color", null, null);

        var response = await _client.PostAsJsonAsync("/api/bookmarktypes", request, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemResponse>(JsonOptions);
        Assert.NotNull(problem);
        Assert.True(problem.Errors.ContainsKey("Color"));
    }

    [Fact]
    public async Task Post_WithIconButNoContentType_ReturnsValidationProblem()
    {
        var request = new CreateBookmarkTypeEndpoint.CreateBookmarkTypeRequest(
            $"{TitlePrefix} BadIcon", null, Convert.ToBase64String([1, 2, 3]), null);

        var response = await _client.PostAsJsonAsync("/api/bookmarktypes", request, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemResponse>(JsonOptions);
        Assert.NotNull(problem);
        Assert.True(problem.Errors.ContainsKey("IconBase64"));
    }

    [Fact]
    public async Task Post_WithInvalidBase64Icon_ReturnsValidationProblem()
    {
        var request = new CreateBookmarkTypeEndpoint.CreateBookmarkTypeRequest(
            $"{TitlePrefix} InvalidBase64", null, "not-valid-base64!!", "image/png");

        var response = await _client.PostAsJsonAsync("/api/bookmarktypes", request, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemResponse>(JsonOptions);
        Assert.NotNull(problem);
        Assert.True(problem.Errors.ContainsKey("IconBase64"));
    }

    private sealed record ValidationProblemResponse(
        string? Type,
        string? Title,
        int? Status,
        Dictionary<string, string[]> Errors);
}
