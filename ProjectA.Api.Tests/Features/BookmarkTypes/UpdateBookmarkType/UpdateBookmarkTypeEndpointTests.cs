using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ProjectA.Api.Data;
using ProjectA.Api.Features.BookmarkTypes.UpdateBookmarkType;
using Xunit;

namespace ProjectA.Api.Tests.Features.BookmarkTypes.UpdateBookmarkType;

[Collection(nameof(ApiCollection))]
public class UpdateBookmarkTypeEndpointTests : IAsyncLifetime
{
    private const string TitlePrefix = "Update Test BookmarkType";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _client;
    private readonly IDbConnectionFactory _connectionFactory;

    public UpdateBookmarkTypeEndpointTests(ApiFactory factory)
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

    private async Task<long> SeedBookmarkTypeAsync(byte[]? icon = null, string? iconContentType = null)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        return await connection.QuerySingleAsync<long>(
            "INSERT INTO bookmark_types (title, color, icon, icon_content_type) " +
            "VALUES (@Title, '#111111', @Icon, @IconContentType) RETURNING id;",
            new { Title = $"{TitlePrefix} Original", Icon = icon, IconContentType = iconContentType });
    }

    [Fact]
    public async Task Put_WithValidRequest_UpdatesAndReturnsOk()
    {
        var id = await SeedBookmarkTypeAsync();
        var request = new UpdateBookmarkTypeEndpoint.UpdateBookmarkTypeRequest(
            $"{TitlePrefix} Updated", "#ABCDEF", null, null, RemoveIcon: false);

        var response = await _client.PutAsJsonAsync($"/api/bookmarktypes/{id}", request, JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await response.Content.ReadFromJsonAsync<UpdateBookmarkTypeEndpoint.BookmarkTypeResponse>(JsonOptions);
        Assert.NotNull(updated);
        Assert.Equal(id, updated.Id);
        Assert.Equal($"{TitlePrefix} Updated", updated.Title);
        Assert.Equal("#ABCDEF", updated.Color);
    }

    [Fact]
    public async Task Put_WhenIdDoesNotExist_ReturnsProblemDetails()
    {
        var request = new UpdateBookmarkTypeEndpoint.UpdateBookmarkTypeRequest(
            $"{TitlePrefix} Missing", null, null, null, RemoveIcon: false);

        var response = await _client.PutAsJsonAsync("/api/bookmarktypes/999999", request, JsonOptions);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Put_WithMissingTitle_ReturnsValidationProblem()
    {
        var id = await SeedBookmarkTypeAsync();
        var request = new UpdateBookmarkTypeEndpoint.UpdateBookmarkTypeRequest(" ", null, null, null, RemoveIcon: false);

        var response = await _client.PutAsJsonAsync($"/api/bookmarktypes/{id}", request, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Put_WithoutIconFields_LeavesExistingIconUnchanged()
    {
        var id = await SeedBookmarkTypeAsync([9, 9, 9], "image/gif");
        var request = new UpdateBookmarkTypeEndpoint.UpdateBookmarkTypeRequest(
            $"{TitlePrefix} Updated", null, null, null, RemoveIcon: false);

        var response = await _client.PutAsJsonAsync($"/api/bookmarktypes/{id}", request, JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await response.Content.ReadFromJsonAsync<UpdateBookmarkTypeEndpoint.BookmarkTypeResponse>(JsonOptions);
        Assert.NotNull(updated);
        Assert.True(updated.HasIcon);

        var iconResponse = await _client.GetAsync($"/api/bookmarktypes/{id}/icon");
        Assert.Equal(HttpStatusCode.OK, iconResponse.StatusCode);
        Assert.Equal(new byte[] { 9, 9, 9 }, await iconResponse.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Put_WithNewIcon_ReplacesExistingIcon()
    {
        var id = await SeedBookmarkTypeAsync([9, 9, 9], "image/gif");
        var newIcon = new byte[] { 4, 5, 6, 7 };
        var request = new UpdateBookmarkTypeEndpoint.UpdateBookmarkTypeRequest(
            $"{TitlePrefix} Updated", null, Convert.ToBase64String(newIcon), "image/png", RemoveIcon: false);

        var response = await _client.PutAsJsonAsync($"/api/bookmarktypes/{id}", request, JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var iconResponse = await _client.GetAsync($"/api/bookmarktypes/{id}/icon");
        Assert.Equal("image/png", iconResponse.Content.Headers.ContentType?.MediaType);
        Assert.Equal(newIcon, await iconResponse.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Put_WithRemoveIcon_ClearsExistingIcon()
    {
        var id = await SeedBookmarkTypeAsync([9, 9, 9], "image/gif");
        var request = new UpdateBookmarkTypeEndpoint.UpdateBookmarkTypeRequest(
            $"{TitlePrefix} Updated", null, null, null, RemoveIcon: true);

        var response = await _client.PutAsJsonAsync($"/api/bookmarktypes/{id}", request, JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await response.Content.ReadFromJsonAsync<UpdateBookmarkTypeEndpoint.BookmarkTypeResponse>(JsonOptions);
        Assert.NotNull(updated);
        Assert.False(updated.HasIcon);

        var iconResponse = await _client.GetAsync($"/api/bookmarktypes/{id}/icon");
        Assert.Equal(HttpStatusCode.NotFound, iconResponse.StatusCode);
    }
}
