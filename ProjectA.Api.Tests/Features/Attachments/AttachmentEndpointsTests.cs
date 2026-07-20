using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ProjectA.Api.Data;
using ProjectA.Api.Features.Attachments.GetAttachmentById;
using ProjectA.Api.Features.Attachments.GetAttachmentList;
using Xunit;

namespace ProjectA.Api.Tests.Features.Attachments;

[Collection(nameof(ApiCollection))]
public class AttachmentEndpointsTests : IAsyncLifetime
{
    private const string TitlePrefix = "List Test Attachment";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _client;
    private readonly IDbConnectionFactory _connectionFactory;

    public AttachmentEndpointsTests(ApiFactory factory)
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
            "DELETE FROM attachments WHERE title LIKE @Pattern;",
            new { Pattern = $"{TitlePrefix}%" });
    }

    [Fact]
    public async Task GetList_ReturnsAllAttachments()
    {
        using (var connection = await _connectionFactory.CreateConnectionAsync())
        {
            await connection.ExecuteAsync(
                "INSERT INTO attachments (title, file_path) VALUES (@TitleA, '/files/a.pdf'), (@TitleB, '/files/b.pdf');",
                new { TitleA = $"{TitlePrefix} A", TitleB = $"{TitlePrefix} B" });
        }

        var response = await _client.GetAsync("/api/attachments");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var attachments = await response.Content
            .ReadFromJsonAsync<List<GetAttachmentListEndpoint.AttachmentListItemResponse>>(JsonOptions);
        Assert.NotNull(attachments);

        var seeded = attachments.Where(a => a.Title != null && a.Title.StartsWith(TitlePrefix)).ToList();
        Assert.Equal(2, seeded.Count);
        Assert.Contains(seeded, a => a.Title == $"{TitlePrefix} A" && a.FilePath == "/files/a.pdf");
        Assert.Contains(seeded, a => a.Title == $"{TitlePrefix} B" && a.FilePath == "/files/b.pdf");
    }

    [Fact]
    public async Task GetById_WhenExists_ReturnsAttachment()
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        var id = await connection.QuerySingleAsync<long>(
            "INSERT INTO attachments (title, file_path) VALUES (@Title, '/files/byid.pdf') RETURNING id;",
            new { Title = $"{TitlePrefix} ById" });

        var response = await _client.GetAsync($"/api/attachments/{id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var attachment = await response.Content
            .ReadFromJsonAsync<GetAttachmentByIdEndpoint.AttachmentResponse>(JsonOptions);
        Assert.NotNull(attachment);
        Assert.Equal(id, attachment.Id);
        Assert.Equal("/files/byid.pdf", attachment.FilePath);
    }

    [Fact]
    public async Task GetById_WhenMissing_ReturnsProblemDetails()
    {
        var response = await _client.GetAsync("/api/attachments/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}
