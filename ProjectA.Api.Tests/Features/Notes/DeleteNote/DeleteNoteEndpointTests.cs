using System.Net;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ProjectA.Api.Data;
using Xunit;

namespace ProjectA.Api.Tests.Features.Notes.DeleteNote;

[Collection(nameof(ApiCollection))]
public class DeleteNoteEndpointTests : IAsyncLifetime
{
    private const string TitlePrefix = "Delete Test Note";
    private const string LinkedBookmarkUrl = "https://example.com/delete-test-note-bookmark";
    private const string LinkedAttachmentTitle = "Delete Test Note Attachment";

    private readonly HttpClient _client;
    private readonly IDbConnectionFactory _connectionFactory;

    public DeleteNoteEndpointTests(ApiFactory factory)
    {
        _client = factory.CreateAuthenticatedClient();
        _connectionFactory = factory.Services.GetRequiredService<IDbConnectionFactory>();
    }

    public async Task InitializeAsync() => await CleanUpAsync();

    public async Task DisposeAsync() => await CleanUpAsync();

    private async Task CleanUpAsync()
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();

        // Delete the join rows first - otherwise the FK constraint this cleanup exists to
        // work around would stop the note/project/category/bookmark/attachment rows
        // themselves being removed.
        await connection.ExecuteAsync(
            "DELETE FROM project_notes WHERE " +
            "note_id IN (SELECT id FROM notes WHERE title LIKE @Pattern) OR " +
            "project_id IN (SELECT id FROM projects WHERE title LIKE @Pattern);",
            new { Pattern = $"{TitlePrefix}%" });
        await connection.ExecuteAsync(
            "DELETE FROM note_categories WHERE " +
            "note_id IN (SELECT id FROM notes WHERE title LIKE @Pattern) OR " +
            "category_id IN (SELECT id FROM categories WHERE title LIKE @Pattern);",
            new { Pattern = $"{TitlePrefix}%" });
        await connection.ExecuteAsync(
            "DELETE FROM note_bookmarks WHERE " +
            "note_id IN (SELECT id FROM notes WHERE title LIKE @Pattern) OR bookmark_id IN " +
            "(SELECT id FROM bookmarks WHERE url = @BookmarkUrl);",
            new { Pattern = $"{TitlePrefix}%", BookmarkUrl = LinkedBookmarkUrl });
        await connection.ExecuteAsync(
            "DELETE FROM note_attachments WHERE " +
            "note_id IN (SELECT id FROM notes WHERE title LIKE @Pattern) OR attachment_id IN " +
            "(SELECT id FROM attachments WHERE title = @AttachmentTitle);",
            new { Pattern = $"{TitlePrefix}%", AttachmentTitle = LinkedAttachmentTitle });
        await connection.ExecuteAsync("DELETE FROM projects WHERE title LIKE @Pattern;", new { Pattern = $"{TitlePrefix}%" });
        await connection.ExecuteAsync("DELETE FROM notes WHERE title LIKE @Pattern;", new { Pattern = $"{TitlePrefix}%" });
        await connection.ExecuteAsync("DELETE FROM categories WHERE title LIKE @Pattern;", new { Pattern = $"{TitlePrefix}%" });
        await connection.ExecuteAsync("DELETE FROM bookmarks WHERE url = @BookmarkUrl;", new { BookmarkUrl = LinkedBookmarkUrl });
        await connection.ExecuteAsync("DELETE FROM attachments WHERE title = @AttachmentTitle;", new { AttachmentTitle = LinkedAttachmentTitle });
    }

    private async Task<long> SeedNoteAsync(string suffix)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        return await connection.QuerySingleAsync<long>(
            "INSERT INTO notes (title) VALUES (@Title) RETURNING id;",
            new { Title = $"{TitlePrefix} {suffix}" });
    }

    [Fact]
    public async Task Delete_WhenExists_RemovesNoteAndReturnsNoContent()
    {
        var id = await SeedNoteAsync("ToDelete");

        var response = await _client.DeleteAsync($"/api/notes/{id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var followUp = await _client.GetAsync($"/api/notes/{id}");
        Assert.Equal(HttpStatusCode.NotFound, followUp.StatusCode);
    }

    [Fact]
    public async Task Delete_WhenMissing_ReturnsProblemDetails()
    {
        var response = await _client.DeleteAsync("/api/notes/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Delete_WhenReferencedByProject_ReturnsConflict()
    {
        var noteId = await SeedNoteAsync("InUse");

        using (var connection = await _connectionFactory.CreateConnectionAsync())
        {
            var projectId = await connection.QuerySingleAsync<long>(
                "INSERT INTO projects (title, start_date) VALUES (@Title, CURRENT_DATE) RETURNING id;",
                new { Title = $"{TitlePrefix} Linking Project" });

            await connection.ExecuteAsync(
                "INSERT INTO project_notes (project_id, note_id) VALUES (@ProjectId, @NoteId);",
                new { ProjectId = projectId, NoteId = noteId });
        }

        var response = await _client.DeleteAsync($"/api/notes/{noteId}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Delete_WhenHasCategoryAssociation_SucceedsAndLeavesCategoryIntact()
    {
        var noteId = await SeedNoteAsync("Categorized");

        long categoryId;
        using (var connection = await _connectionFactory.CreateConnectionAsync())
        {
            categoryId = await connection.QuerySingleAsync<long>(
                "INSERT INTO categories (title) VALUES (@Title) RETURNING id;",
                new { Title = $"{TitlePrefix} Category" });

            await connection.ExecuteAsync(
                "INSERT INTO note_categories (note_id, category_id) VALUES (@NoteId, @CategoryId);",
                new { NoteId = noteId, CategoryId = categoryId });
        }

        // The category association must not block deletion...
        var response = await _client.DeleteAsync($"/api/notes/{noteId}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // ...and the category itself must survive, untouched.
        var categoryResponse = await _client.GetAsync($"/api/categories/{categoryId}");
        Assert.Equal(HttpStatusCode.OK, categoryResponse.StatusCode);

        // The join row should be gone too, not left dangling.
        using var verifyConnection = await _connectionFactory.CreateConnectionAsync();
        var remainingLinks = await verifyConnection.QuerySingleAsync<long>(
            "SELECT COUNT(*) FROM note_categories WHERE note_id = @NoteId;",
            new { NoteId = noteId });
        Assert.Equal(0, remainingLinks);
    }

    [Fact]
    public async Task Delete_WhenHasBookmarkAndAttachmentAssociations_SucceedsAndLeavesThemIntact()
    {
        var noteId = await SeedNoteAsync("WithBookmarkAndAttachment");

        long bookmarkId;
        long attachmentId;
        using (var connection = await _connectionFactory.CreateConnectionAsync())
        {
            bookmarkId = await connection.QuerySingleAsync<long>(
                "INSERT INTO bookmarks (url) VALUES (@Url) RETURNING id;",
                new { Url = LinkedBookmarkUrl });
            attachmentId = await connection.QuerySingleAsync<long>(
                "INSERT INTO attachments (title, s3_arn) VALUES (@Title, 'arn:aws:s3:::test/bucket') RETURNING id;",
                new { Title = LinkedAttachmentTitle });

            await connection.ExecuteAsync(
                "INSERT INTO note_bookmarks (note_id, bookmark_id) VALUES (@NoteId, @BookmarkId);",
                new { NoteId = noteId, BookmarkId = bookmarkId });
            await connection.ExecuteAsync(
                "INSERT INTO note_attachments (note_id, attachment_id) VALUES (@NoteId, @AttachmentId);",
                new { NoteId = noteId, AttachmentId = attachmentId });
        }

        // Neither association should block deletion...
        var response = await _client.DeleteAsync($"/api/notes/{noteId}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // ...and the bookmark and attachment themselves must survive, untouched.
        var bookmarkResponse = await _client.GetAsync($"/api/bookmarks/{bookmarkId}");
        Assert.Equal(HttpStatusCode.OK, bookmarkResponse.StatusCode);
        var attachmentResponse = await _client.GetAsync($"/api/attachments/{attachmentId}");
        Assert.Equal(HttpStatusCode.OK, attachmentResponse.StatusCode);

        // The join rows should be gone too, not left dangling.
        using var verifyConnection = await _connectionFactory.CreateConnectionAsync();
        var remainingBookmarkLinks = await verifyConnection.QuerySingleAsync<long>(
            "SELECT COUNT(*) FROM note_bookmarks WHERE note_id = @NoteId;",
            new { NoteId = noteId });
        var remainingAttachmentLinks = await verifyConnection.QuerySingleAsync<long>(
            "SELECT COUNT(*) FROM note_attachments WHERE note_id = @NoteId;",
            new { NoteId = noteId });
        Assert.Equal(0, remainingBookmarkLinks);
        Assert.Equal(0, remainingAttachmentLinks);
    }
}
