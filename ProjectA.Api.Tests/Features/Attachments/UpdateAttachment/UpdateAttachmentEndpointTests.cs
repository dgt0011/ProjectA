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

        // AttachmentTypeId is a plain column on attachments (no join table involved) - cleared
        // here, matched by attachment_type_id rather than the referencing attachment's title, so
        // the attachment_types delete below is never blocked by an attachment whose title
        // doesn't happen to match the pattern (e.g. after a test PUTs a null Title).
        await connection.ExecuteAsync(
            "UPDATE attachments SET attachment_type_id = NULL WHERE attachment_type_id IN " +
            "(SELECT id FROM attachment_types WHERE title LIKE @Pattern);",
            new { Pattern = $"{TitlePrefix} %" });
        await connection.ExecuteAsync(
            "DELETE FROM attachments WHERE title LIKE @Pattern;",
            new { Pattern = $"{TitlePrefix}%" });
        await connection.ExecuteAsync(
            "DELETE FROM attachment_types WHERE title LIKE @Pattern;",
            new { Pattern = $"{TitlePrefix} %" });
    }

    private async Task<long> SeedAttachmentAsync()
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        return await connection.QuerySingleAsync<long>(
            "INSERT INTO attachments (title, description, file_path) " +
            "VALUES (@Title, 'Original description', '/files/original.pdf') RETURNING id;",
            new { Title = $"{TitlePrefix} Original" });
    }

    private async Task<long> SeedAttachmentTypeAsync(string suffix)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        return await connection.QuerySingleAsync<long>(
            "INSERT INTO attachment_types (title) VALUES (@Title) RETURNING id;",
            new { Title = $"{TitlePrefix} {suffix}" });
    }

    [Fact]
    public async Task Put_WithValidRequest_UpdatesAndReturnsOk()
    {
        var id = await SeedAttachmentAsync();
        var request = new UpdateAttachmentEndpoint.UpdateAttachmentRequest(
            $"{TitlePrefix} Updated", "Updated description", "/files/updated.pdf", null);

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
        var request = new UpdateAttachmentEndpoint.UpdateAttachmentRequest(null, null, "/files/missing.pdf", null);

        var response = await _client.PutAsJsonAsync("/api/attachments/999999", request, JsonOptions);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Put_WithMissingFilePath_ReturnsValidationProblem()
    {
        var id = await SeedAttachmentAsync();
        var request = new UpdateAttachmentEndpoint.UpdateAttachmentRequest(null, null, " ", null);

        var response = await _client.PutAsJsonAsync($"/api/attachments/{id}", request, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Put_WithAttachmentTypeId_SetsAttachmentType()
    {
        var id = await SeedAttachmentAsync();
        var attachmentTypeId = await SeedAttachmentTypeAsync("Invoice");

        var request = new UpdateAttachmentEndpoint.UpdateAttachmentRequest(
            $"{TitlePrefix} Original", null, "/files/original.pdf", attachmentTypeId);

        var response = await _client.PutAsJsonAsync($"/api/attachments/{id}", request, JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await response.Content.ReadFromJsonAsync<UpdateAttachmentEndpoint.AttachmentResponse>(JsonOptions);
        Assert.NotNull(updated);
        Assert.Equal(attachmentTypeId, updated.AttachmentTypeId);
    }

    [Fact]
    public async Task Put_WithNullAttachmentTypeId_ClearsExistingAttachmentType()
    {
        var attachmentTypeId = await SeedAttachmentTypeAsync("ToClear");
        long attachmentId;
        using (var connection = await _connectionFactory.CreateConnectionAsync())
        {
            attachmentId = await connection.QuerySingleAsync<long>(
                "INSERT INTO attachments (title, file_path, attachment_type_id) VALUES " +
                "(@Title, '/files/original.pdf', @AttachmentTypeId) RETURNING id;",
                new { Title = $"{TitlePrefix} Typed", AttachmentTypeId = attachmentTypeId });
        }

        var request = new UpdateAttachmentEndpoint.UpdateAttachmentRequest(
            $"{TitlePrefix} Typed", null, "/files/original.pdf", null);

        var response = await _client.PutAsJsonAsync($"/api/attachments/{attachmentId}", request, JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await response.Content.ReadFromJsonAsync<UpdateAttachmentEndpoint.AttachmentResponse>(JsonOptions);
        Assert.NotNull(updated);
        Assert.Null(updated.AttachmentTypeId);
    }

    [Fact]
    public async Task Put_WithInvalidAttachmentTypeId_ReturnsValidationProblem_AndLeavesAttachmentUnchanged()
    {
        var id = await SeedAttachmentAsync();
        var request = new UpdateAttachmentEndpoint.UpdateAttachmentRequest(
            $"{TitlePrefix} Original", null, "/files/should-not-apply.pdf", 999999);

        var response = await _client.PutAsJsonAsync($"/api/attachments/{id}", request, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var connection = await _connectionFactory.CreateConnectionAsync();
        var filePath = await connection.QuerySingleAsync<string>(
            "SELECT file_path FROM attachments WHERE id = @Id;", new { Id = id });
        Assert.Equal("/files/original.pdf", filePath);
    }
}
