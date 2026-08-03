using System.Net;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ProjectA.Api.Data;
using Xunit;

namespace ProjectA.Api.Tests.Features.AttachmentTypes.DeleteAttachmentType;

[Collection(nameof(ApiCollection))]
public class DeleteAttachmentTypeEndpointTests : IAsyncLifetime
{
    private const string TitlePrefix = "Delete Test AttachmentType";

    private readonly HttpClient _client;
    private readonly IDbConnectionFactory _connectionFactory;

    public DeleteAttachmentTypeEndpointTests(ApiFactory factory)
    {
        _client = factory.CreateAuthenticatedClient();
        _connectionFactory = factory.Services.GetRequiredService<IDbConnectionFactory>();
    }

    public async Task InitializeAsync() => await CleanUpAsync();

    public async Task DisposeAsync() => await CleanUpAsync();

    private async Task CleanUpAsync()
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();

        // Clear any blocking attachments first - otherwise the FK constraint this cleanup
        // exists to work around would stop the attachment_types rows themselves being removed.
        await connection.ExecuteAsync(
            "UPDATE attachments SET attachment_type_id = NULL WHERE attachment_type_id IN " +
            "(SELECT id FROM attachment_types WHERE title LIKE @Pattern);",
            new { Pattern = $"{TitlePrefix}%" });
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
    public async Task Delete_WhenExists_RemovesAttachmentTypeAndReturnsNoContent()
    {
        var id = await SeedAttachmentTypeAsync("ToDelete");

        var response = await _client.DeleteAsync($"/api/attachmenttypes/{id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var followUp = await _client.GetAsync($"/api/attachmenttypes/{id}");
        Assert.Equal(HttpStatusCode.NotFound, followUp.StatusCode);
    }

    [Fact]
    public async Task Delete_WhenMissing_ReturnsProblemDetails()
    {
        var response = await _client.DeleteAsync("/api/attachmenttypes/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Delete_WhenAssignedToAttachment_ReturnsConflict()
    {
        var id = await SeedAttachmentTypeAsync("InUse");

        using (var connection = await _connectionFactory.CreateConnectionAsync())
        {
            await connection.ExecuteAsync(
                "INSERT INTO attachments (title, file_path, attachment_type_id) VALUES (@Title, '/files/x.pdf', @AttachmentTypeId);",
                new { Title = $"{TitlePrefix} Blocking Attachment", AttachmentTypeId = id });
        }

        var response = await _client.DeleteAsync($"/api/attachmenttypes/{id}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}
