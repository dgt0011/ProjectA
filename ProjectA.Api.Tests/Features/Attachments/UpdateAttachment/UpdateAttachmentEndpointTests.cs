using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ProjectA.Api.Data;
using ProjectA.Api.Features.Attachments.UpdateAttachment;
using Xunit;

namespace ProjectA.Api.Tests.Features.Attachments.UpdateAttachment;

[Collection(nameof(ApiCollection))]
public class UpdateAttachmentEndpointTests : IAsyncLifetime
{
    private const string TitlePrefix = "Update Test Attachment";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _client;
    private readonly IDbConnectionFactory _connectionFactory;

    public UpdateAttachmentEndpointTests(ApiFactory factory)
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

    private async Task<long> SeedAttachmentAsync()
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        return await connection.QuerySingleAsync<long>(
            "INSERT INTO attachments (title, description, file_path) " +
            "VALUES (@Title, 'Original description', '/files/original.pdf') RETURNING id;",
            new { Title = $"{TitlePrefix} Original" });
    }

    [Fact]
    public async Task Put_WithValidRequest_UpdatesAndReturnsOk()
    {
        var id = await SeedAttachmentAsync();
        var request = new UpdateAttachmentEndpoint.UpdateAttachmentRequest(
            $"{TitlePrefix} Updated", "Updated description", "/files/updated.pdf");

        var response = await _client.PutAsJsonAsync($"/api/attachments/{id}", request, JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await response.Content.ReadFromJsonAsync<UpdateAttachmentEndpoint.AttachmentResponse>(JsonOptions);
        Assert.NotNull(updated);
        Assert.Equal(id, updated.Id);
        Assert.Equal("/files/updated.pdf", updated.FilePath);
        Assert.NotNull(updated.DateModified);
    }

    [Fact]
    public async Task Put_WhenIdDoesNotExist_ReturnsProblemDetails()
    {
        var request = new UpdateAttachmentEndpoint.UpdateAttachmentRequest(null, null, "/files/missing.pdf");

        var response = await _client.PutAsJsonAsync("/api/attachments/999999", request, JsonOptions);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Put_WithMissingFilePath_ReturnsValidationProblem()
    {
        var id = await SeedAttachmentAsync();
        var request = new UpdateAttachmentEndpoint.UpdateAttachmentRequest(null, null, " ");

        var response = await _client.PutAsJsonAsync($"/api/attachments/{id}", request, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
