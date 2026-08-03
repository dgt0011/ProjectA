using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ProjectA.Api.Data;
using ProjectA.Api.Features.AttachmentTypes.GetAttachmentTypeById;
using ProjectA.Api.Features.AttachmentTypes.GetAttachmentTypeList;
using Xunit;

namespace ProjectA.Api.Tests.Features.AttachmentTypes;

[Collection(nameof(ApiCollection))]
public class AttachmentTypeEndpointsTests : IAsyncLifetime
{
    private const string TitlePrefix = "List Test AttachmentType";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _client;
    private readonly IDbConnectionFactory _connectionFactory;

    public AttachmentTypeEndpointsTests(ApiFactory factory)
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
            "DELETE FROM attachment_types WHERE title LIKE @Pattern;",
            new { Pattern = $"{TitlePrefix}%" });
    }

    [Fact]
    public async Task GetList_ReturnsAllAttachmentTypes()
    {
        using (var connection = await _connectionFactory.CreateConnectionAsync())
        {
            await connection.ExecuteAsync(
                "INSERT INTO attachment_types (title, color) VALUES (@TitleA, '#111111'), (@TitleB, NULL);",
                new { TitleA = $"{TitlePrefix} A", TitleB = $"{TitlePrefix} B" });
        }

        var response = await _client.GetAsync("/api/attachmenttypes");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var types = await response.Content
            .ReadFromJsonAsync<List<GetAttachmentTypeListEndpoint.AttachmentTypeListItemResponse>>(JsonOptions);
        Assert.NotNull(types);

        var seeded = types.Where(t => t.Title.StartsWith(TitlePrefix)).ToList();
        Assert.Equal(2, seeded.Count);
        Assert.Contains(seeded, t => t.Title == $"{TitlePrefix} A" && t.Color == "#111111");
        Assert.Contains(seeded, t => t.Title == $"{TitlePrefix} B" && t.Color == null);
        Assert.All(seeded, t => Assert.False(t.HasIcon));
    }

    [Fact]
    public async Task GetById_WhenExists_ReturnsAttachmentType()
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        var id = await connection.QuerySingleAsync<long>(
            "INSERT INTO attachment_types (title, color) VALUES (@Title, '#222222') RETURNING id;",
            new { Title = $"{TitlePrefix} ById" });

        var response = await _client.GetAsync($"/api/attachmenttypes/{id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var type = await response.Content
            .ReadFromJsonAsync<GetAttachmentTypeByIdEndpoint.AttachmentTypeResponse>(JsonOptions);
        Assert.NotNull(type);
        Assert.Equal(id, type.Id);
        Assert.Equal($"{TitlePrefix} ById", type.Title);
        Assert.Equal("#222222", type.Color);
    }

    [Fact]
    public async Task GetById_WhenMissing_ReturnsProblemDetails()
    {
        var response = await _client.GetAsync("/api/attachmenttypes/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task GetIcon_WhenAttachmentTypeHasNoIcon_ReturnsNotFound()
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        var id = await connection.QuerySingleAsync<long>(
            "INSERT INTO attachment_types (title) VALUES (@Title) RETURNING id;",
            new { Title = $"{TitlePrefix} NoIcon" });

        var response = await _client.GetAsync($"/api/attachmenttypes/{id}/icon");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetIcon_WhenAttachmentTypeMissing_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/attachmenttypes/999999/icon");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
