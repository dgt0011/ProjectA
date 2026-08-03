using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ProjectA.Api.Data;
using ProjectA.Api.Features.Attachments.CreateAttachment;
using ProjectA.Api.Features.Attachments.GetAttachmentById;
using Xunit;

namespace ProjectA.Api.Tests.Features.Attachments.CreateAttachment;

[Collection(nameof(ApiCollection))]
public class CreateAttachmentEndpointTests : IAsyncLifetime
{
    private const string TitlePrefix = "Create Test Attachment";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _client;
    private readonly IDbConnectionFactory _connectionFactory;

    public CreateAttachmentEndpointTests(ApiFactory factory)
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
        await connection.ExecuteAsync(
            "DELETE FROM attachment_types WHERE title LIKE @Pattern;",
            new { Pattern = $"{TitlePrefix}%" });
    }

    private async Task<long> SeedAttachmentTypeAsync(string suffix)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        return await connection.QuerySingleAsync<long>(
            "INSERT INTO attachment_types (title) VALUES (@Title) RETURNING id;",
            new { Title = $"{TitlePrefix} {suffix}" });
    }

    [Fact]
    public async Task Post_WithValidRequest_CreatesAttachmentAndReturnsCreated()
    {
        var request = new CreateAttachmentEndpoint.CreateAttachmentRequest(
            $"{TitlePrefix} New", "A description", "/files/new.pdf", null);

        var response = await _client.PostAsJsonAsync("/api/attachments", request, JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var created = await response.Content.ReadFromJsonAsync<CreateAttachmentEndpoint.AttachmentResponse>(JsonOptions);
        Assert.NotNull(created);
        Assert.True(created.Id > 0);
        Assert.Contains($"/api/attachments/{created.Id}", response.Headers.Location!.ToString());
        Assert.Equal("/files/new.pdf", created.FilePath);
    }

    [Fact]
    public async Task Post_WithMissingFilePath_ReturnsValidationProblem()
    {
        var request = new CreateAttachmentEndpoint.CreateAttachmentRequest($"{TitlePrefix} NoPath", null, " ", null);

        var response = await _client.PostAsJsonAsync("/api/attachments", request, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemResponse>(JsonOptions);
        Assert.NotNull(problem);
        Assert.True(problem.Errors.ContainsKey("FilePath"));
    }

    [Fact]
    public async Task Post_WithAttachmentTypeId_AssociatesAttachmentTypeAndReturnsIt()
    {
        var attachmentTypeId = await SeedAttachmentTypeAsync("Invoice");

        var request = new CreateAttachmentEndpoint.CreateAttachmentRequest(
            $"{TitlePrefix} Typed", null, "/files/typed.pdf", attachmentTypeId);

        var response = await _client.PostAsJsonAsync("/api/attachments", request, JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<CreateAttachmentEndpoint.AttachmentResponse>(JsonOptions);
        Assert.NotNull(created);
        Assert.Equal(attachmentTypeId, created.AttachmentTypeId);

        var getResponse = await _client.GetAsync($"/api/attachments/{created.Id}");
        var fetched = await getResponse.Content
            .ReadFromJsonAsync<GetAttachmentByIdEndpoint.AttachmentResponse>(JsonOptions);
        Assert.NotNull(fetched);
        Assert.Equal(attachmentTypeId, fetched.AttachmentTypeId);
    }

    [Fact]
    public async Task Post_WithInvalidAttachmentTypeId_ReturnsValidationProblem_AndCreatesNoAttachment()
    {
        var request = new CreateAttachmentEndpoint.CreateAttachmentRequest(
            $"{TitlePrefix} BadAttachmentType", null, "/files/bad.pdf", 999999);

        var response = await _client.PostAsJsonAsync("/api/attachments", request, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemResponse>(JsonOptions);
        Assert.NotNull(problem);
        Assert.True(problem.Errors.ContainsKey("AttachmentTypeId"));

        using var connection = await _connectionFactory.CreateConnectionAsync();
        var count = await connection.QuerySingleAsync<long>(
            "SELECT COUNT(*) FROM attachments WHERE title = @Title;",
            new { Title = $"{TitlePrefix} BadAttachmentType" });
        Assert.Equal(0, count);
    }

    private sealed record ValidationProblemResponse(
        string? Type,
        string? Title,
        int? Status,
        Dictionary<string, string[]> Errors);
}
