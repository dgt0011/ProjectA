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
        _client = factory.CreateClient();
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
                "INSERT INTO attachments (title, s3_arn) VALUES (@TitleA, 'arn:aws:s3:::bucket/a'), (@TitleB, 'arn:aws:s3:::bucket/b');",
                new { TitleA = $"{TitlePrefix} A", TitleB = $"{TitlePrefix} B" });
        }

        var response = await _client.GetAsync("/api/attachments");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var attachments = await response.Content
            .ReadFromJsonAsync<List<GetAttachmentListEndpoint.AttachmentListItemResponse>>(JsonOptions);
        Assert.NotNull(attachments);

        var seeded = attachments.Where(a => a.Title != null && a.Title.StartsWith(TitlePrefix)).ToList();
        Assert.Equal(2, seeded.Count);
        Assert.Contains(seeded, a => a.Title == $"{TitlePrefix} A" && a.S3Arn == "arn:aws:s3:::bucket/a");
        Assert.Contains(seeded, a => a.Title == $"{TitlePrefix} B" && a.S3Arn == "arn:aws:s3:::bucket/b");
    }

    [Fact]
    public async Task GetById_WhenExists_ReturnsAttachment()
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        var id = await connection.QuerySingleAsync<long>(
            "INSERT INTO attachments (title, s3_arn) VALUES (@Title, 'arn:aws:s3:::bucket/byid') RETURNING id;",
            new { Title = $"{TitlePrefix} ById" });

        var response = await _client.GetAsync($"/api/attachments/{id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var attachment = await response.Content
            .ReadFromJsonAsync<GetAttachmentByIdEndpoint.AttachmentResponse>(JsonOptions);
        Assert.NotNull(attachment);
        Assert.Equal(id, attachment.Id);
        Assert.Equal("arn:aws:s3:::bucket/byid", attachment.S3Arn);
    }

    [Fact]
    public async Task GetById_WhenMissing_ReturnsProblemDetails()
    {
        var response = await _client.GetAsync("/api/attachments/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}
