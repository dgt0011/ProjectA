using System.Net;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ProjectA.Api.Data;
using Xunit;

namespace ProjectA.Api.Tests.Features.Attachments.DeleteAttachment;

[Collection(nameof(ApiCollection))]
public class DeleteAttachmentEndpointTests : IAsyncLifetime
{
    private const string TitlePrefix = "Delete Test Attachment";

    private readonly HttpClient _client;
    private readonly IDbConnectionFactory _connectionFactory;

    public DeleteAttachmentEndpointTests(ApiFactory factory)
    {
        _client = factory.CreateClient();
        _connectionFactory = factory.Services.GetRequiredService<IDbConnectionFactory>();
    }

    public async Task InitializeAsync() => await CleanUpAsync();

    public async Task DisposeAsync() => await CleanUpAsync();

    private async Task CleanUpAsync()
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();

        // Delete the join rows first - otherwise the FK constraint this cleanup exists to
        // work around would stop the attachment/note rows themselves being removed.
        await connection.ExecuteAsync(
            "DELETE FROM note_attachments WHERE " +
            "attachment_id IN (SELECT id FROM attachments WHERE title LIKE @Pattern) OR " +
            "note_id IN (SELECT id FROM notes WHERE title LIKE @Pattern);",
            new { Pattern = $"{TitlePrefix}%" });
        await connection.ExecuteAsync("DELETE FROM notes WHERE title LIKE @Pattern;", new { Pattern = $"{TitlePrefix}%" });
        await connection.ExecuteAsync("DELETE FROM attachments WHERE title LIKE @Pattern;", new { Pattern = $"{TitlePrefix}%" });
    }

    private async Task<long> SeedAttachmentAsync(string suffix)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        return await connection.QuerySingleAsync<long>(
            "INSERT INTO attachments (title, s3_arn) VALUES (@Title, 'arn:aws:s3:::bucket/x') RETURNING id;",
            new { Title = $"{TitlePrefix} {suffix}" });
    }

    [Fact]
    public async Task Delete_WhenExists_RemovesAttachmentAndReturnsNoContent()
    {
        var id = await SeedAttachmentAsync("ToDelete");

        var response = await _client.DeleteAsync($"/api/attachments/{id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var followUp = await _client.GetAsync($"/api/attachments/{id}");
        Assert.Equal(HttpStatusCode.NotFound, followUp.StatusCode);
    }

    [Fact]
    public async Task Delete_WhenMissing_ReturnsProblemDetails()
    {
        var response = await _client.DeleteAsync("/api/attachments/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Delete_WhenReferencedByNote_ReturnsConflict()
    {
        var attachmentId = await SeedAttachmentAsync("InUse");

        using (var connection = await _connectionFactory.CreateConnectionAsync())
        {
            var noteId = await connection.QuerySingleAsync<long>(
                "INSERT INTO notes (title) VALUES (@Title) RETURNING id;",
                new { Title = $"{TitlePrefix} Linking Note" });

            await connection.ExecuteAsync(
                "INSERT INTO note_attachments (note_id, attachment_id) VALUES (@NoteId, @AttachmentId);",
                new { NoteId = noteId, AttachmentId = attachmentId });
        }

        var response = await _client.DeleteAsync($"/api/attachments/{attachmentId}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}
