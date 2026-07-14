using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ProjectA.Api.Data;
using ProjectA.Api.Features.BookmarkTypes.GetBookmarkTypeById;
using ProjectA.Api.Features.BookmarkTypes.GetBookmarkTypeList;
using Xunit;

namespace ProjectA.Api.Tests.Features.BookmarkTypes;

[Collection(nameof(ApiCollection))]
public class BookmarkTypeEndpointsTests : IAsyncLifetime
{
    private const string TitlePrefix = "List Test BookmarkType";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _client;
    private readonly IDbConnectionFactory _connectionFactory;

    public BookmarkTypeEndpointsTests(ApiFactory factory)
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
    public async Task GetList_ReturnsAllBookmarkTypes()
    {
        using (var connection = await _connectionFactory.CreateConnectionAsync())
        {
            await connection.ExecuteAsync(
                "INSERT INTO bookmark_types (title, color) VALUES (@TitleA, '#111111'), (@TitleB, NULL);",
                new { TitleA = $"{TitlePrefix} A", TitleB = $"{TitlePrefix} B" });
        }

        var response = await _client.GetAsync("/api/bookmarktypes");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var types = await response.Content
            .ReadFromJsonAsync<List<GetBookmarkTypeListEndpoint.BookmarkTypeListItemResponse>>(JsonOptions);
        Assert.NotNull(types);

        var seeded = types.Where(t => t.Title.StartsWith(TitlePrefix)).ToList();
        Assert.Equal(2, seeded.Count);
        Assert.Contains(seeded, t => t.Title == $"{TitlePrefix} A" && t.Color == "#111111");
        Assert.Contains(seeded, t => t.Title == $"{TitlePrefix} B" && t.Color == null);
        Assert.All(seeded, t => Assert.False(t.HasIcon));
    }

    [Fact]
    public async Task GetById_WhenExists_ReturnsBookmarkType()
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        var id = await connection.QuerySingleAsync<long>(
            "INSERT INTO bookmark_types (title, color) VALUES (@Title, '#222222') RETURNING id;",
            new { Title = $"{TitlePrefix} ById" });

        var response = await _client.GetAsync($"/api/bookmarktypes/{id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var type = await response.Content
            .ReadFromJsonAsync<GetBookmarkTypeByIdEndpoint.BookmarkTypeResponse>(JsonOptions);
        Assert.NotNull(type);
        Assert.Equal(id, type.Id);
        Assert.Equal($"{TitlePrefix} ById", type.Title);
        Assert.Equal("#222222", type.Color);
    }

    [Fact]
    public async Task GetById_WhenMissing_ReturnsProblemDetails()
    {
        var response = await _client.GetAsync("/api/bookmarktypes/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task GetIcon_WhenBookmarkTypeHasNoIcon_ReturnsNotFound()
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        var id = await connection.QuerySingleAsync<long>(
            "INSERT INTO bookmark_types (title) VALUES (@Title) RETURNING id;",
            new { Title = $"{TitlePrefix} NoIcon" });

        var response = await _client.GetAsync($"/api/bookmarktypes/{id}/icon");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetIcon_WhenBookmarkTypeMissing_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/bookmarktypes/999999/icon");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
