using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ProjectA.Api.Data;
using ProjectA.Api.Features.Notes.UpdateNote;
using Xunit;

namespace ProjectA.Api.Tests.Features.Notes.UpdateNote;

[Collection(nameof(ApiCollection))]
public class UpdateNoteEndpointTests : IAsyncLifetime
{
    private const string TitlePrefix = "Update Test Note";
    private const string LinkedBookmarkUrl = "https://example.com/update-test-note-bookmark";
    private const string LinkedAttachmentTitle = "Update Test Note Attachment";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _client;
    private readonly IDbConnectionFactory _connectionFactory;

    public UpdateNoteEndpointTests(ApiFactory factory)
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
            "DELETE FROM note_bookmarks WHERE note_id IN (SELECT id FROM notes WHERE title LIKE @Pattern) OR " +
            "bookmark_id IN (SELECT id FROM bookmarks WHERE url = @BookmarkUrl);",
            new { Pattern = $"{TitlePrefix}%", BookmarkUrl = LinkedBookmarkUrl });
        await connection.ExecuteAsync(
            "DELETE FROM note_attachments WHERE note_id IN (SELECT id FROM notes WHERE title LIKE @Pattern) OR " +
            "attachment_id IN (SELECT id FROM attachments WHERE title = @AttachmentTitle);",
            new { Pattern = $"{TitlePrefix}%", AttachmentTitle = LinkedAttachmentTitle });
        await connection.ExecuteAsync("DELETE FROM notes WHERE title LIKE @Pattern;", new { Pattern = $"{TitlePrefix}%" });
        await connection.ExecuteAsync("DELETE FROM bookmarks WHERE url = @BookmarkUrl;", new { BookmarkUrl = LinkedBookmarkUrl });
        await connection.ExecuteAsync("DELETE FROM attachments WHERE title = @AttachmentTitle;", new { AttachmentTitle = LinkedAttachmentTitle });
    }

    private async Task<long> SeedNoteAsync()
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        return await connection.QuerySingleAsync<long>(
            "INSERT INTO notes (title, body) VALUES (@Title, 'Original body') RETURNING id;",
            new { Title = $"{TitlePrefix} Original" });
    }

    [Fact]
    public async Task Put_WithValidRequest_UpdatesAndReturnsOk()
    {
        var id = await SeedNoteAsync();
        var request = new UpdateNoteEndpoint.UpdateNoteRequest($"{TitlePrefix} Updated", "Updated summary", "Updated body", null, null, null);

        var response = await _client.PutAsJsonAsync($"/api/notes/{id}", request, JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await response.Content.ReadFromJsonAsync<UpdateNoteEndpoint.NoteResponse>(JsonOptions);
        Assert.NotNull(updated);
        Assert.Equal(id, updated.Id);
        Assert.Equal($"{TitlePrefix} Updated", updated.Title);
        Assert.Equal("Updated summary", updated.Description);
        Assert.Equal("Updated body", updated.Body);
        Assert.NotNull(updated.DateModified);
    }

    [Fact]
    public async Task Put_WhenIdDoesNotExist_ReturnsProblemDetails()
    {
        var request = new UpdateNoteEndpoint.UpdateNoteRequest($"{TitlePrefix} Missing", null, null, null, null, null);

        var response = await _client.PutAsJsonAsync("/api/notes/999999", request, JsonOptions);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Put_WithNeitherTitleNorBody_ReturnsValidationProblem()
    {
        var id = await SeedNoteAsync();
        var request = new UpdateNoteEndpoint.UpdateNoteRequest(null, null, null, null, null, null);

        var response = await _client.PutAsJsonAsync($"/api/notes/{id}", request, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Put_WithBookmarkIdsNull_LeavesAssociationsUnchanged()
    {
        var id = await SeedNoteAsync();
        long bookmarkId;
        using (var connection = await _connectionFactory.CreateConnectionAsync())
        {
            bookmarkId = await connection.QuerySingleAsync<long>(
                "INSERT INTO bookmarks (url) VALUES (@Url) RETURNING id;",
                new { Url = LinkedBookmarkUrl });
            await connection.ExecuteAsync(
                "INSERT INTO note_bookmarks (note_id, bookmark_id) VALUES (@NoteId, @BookmarkId);",
                new { NoteId = id, BookmarkId = bookmarkId });
        }

        var request = new UpdateNoteEndpoint.UpdateNoteRequest($"{TitlePrefix} Updated", null, "Updated body", null, null, null);

        var response = await _client.PutAsJsonAsync($"/api/notes/{id}", request, JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await response.Content.ReadFromJsonAsync<UpdateNoteEndpoint.NoteResponse>(JsonOptions);
        Assert.NotNull(updated);
        Assert.Equal([bookmarkId], updated.BookmarkIds);
    }

    [Fact]
    public async Task Put_WithEmptyBookmarkIds_ClearsAssociations()
    {
        var id = await SeedNoteAsync();
        using (var connection = await _connectionFactory.CreateConnectionAsync())
        {
            var bookmarkId = await connection.QuerySingleAsync<long>(
                "INSERT INTO bookmarks (url) VALUES (@Url) RETURNING id;",
                new { Url = LinkedBookmarkUrl });
            await connection.ExecuteAsync(
                "INSERT INTO note_bookmarks (note_id, bookmark_id) VALUES (@NoteId, @BookmarkId);",
                new { NoteId = id, BookmarkId = bookmarkId });
        }

        var request = new UpdateNoteEndpoint.UpdateNoteRequest($"{TitlePrefix} Updated", null, "Updated body", null, [], null);

        var response = await _client.PutAsJsonAsync($"/api/notes/{id}", request, JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await response.Content.ReadFromJsonAsync<UpdateNoteEndpoint.NoteResponse>(JsonOptions);
        Assert.NotNull(updated);
        Assert.Empty(updated.BookmarkIds);
    }

    [Fact]
    public async Task Put_WithAttachmentIdThatDoesNotExist_ReturnsValidationProblem()
    {
        var id = await SeedNoteAsync();
        var request = new UpdateNoteEndpoint.UpdateNoteRequest($"{TitlePrefix} Updated", null, "Updated body", null, null, [999999]);

        var response = await _client.PutAsJsonAsync($"/api/notes/{id}", request, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Put_WithParentNoteId_SetsParent()
    {
        var id = await SeedNoteAsync();
        using var connection = await _connectionFactory.CreateConnectionAsync();
        var parentId = await connection.QuerySingleAsync<long>(
            "INSERT INTO notes (title) VALUES (@Title) RETURNING id;",
            new { Title = $"{TitlePrefix} Parent" });

        var request = new UpdateNoteEndpoint.UpdateNoteRequest(
            $"{TitlePrefix} Updated", null, "Updated body", parentId, null, null);

        var response = await _client.PutAsJsonAsync($"/api/notes/{id}", request, JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await response.Content.ReadFromJsonAsync<UpdateNoteEndpoint.NoteResponse>(JsonOptions);
        Assert.NotNull(updated);
        Assert.Equal(parentId, updated.ParentNoteId);
    }

    [Fact]
    public async Task Put_WithParentNoteIdThatDoesNotExist_ReturnsValidationProblem()
    {
        var id = await SeedNoteAsync();
        var request = new UpdateNoteEndpoint.UpdateNoteRequest(
            $"{TitlePrefix} Updated", null, "Updated body", 999999, null, null);

        var response = await _client.PutAsJsonAsync($"/api/notes/{id}", request, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemResponse>(JsonOptions);
        Assert.NotNull(problem);
        Assert.True(problem.Errors.ContainsKey("ParentNoteId"));
    }

    [Fact]
    public async Task Put_WithSelfAsParentNoteId_ReturnsValidationProblem()
    {
        var id = await SeedNoteAsync();
        var request = new UpdateNoteEndpoint.UpdateNoteRequest($"{TitlePrefix} Updated", null, "Updated body", id, null, null);

        var response = await _client.PutAsJsonAsync($"/api/notes/{id}", request, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemResponse>(JsonOptions);
        Assert.NotNull(problem);
        Assert.True(problem.Errors.ContainsKey("ParentNoteId"));
    }

    [Fact]
    public async Task Put_WithDescendantAsParentNoteId_ReturnsValidationProblem()
    {
        // grandparent -> parent -> child, then try to make the grandparent a child of its
        // own grandchild - that would create a cycle.
        var grandparentId = await SeedNoteAsync();

        using var connection = await _connectionFactory.CreateConnectionAsync();
        var childId = await connection.QuerySingleAsync<long>(
            "INSERT INTO notes (title, parent_note_id) VALUES (@Title, @ParentNoteId) RETURNING id;",
            new { Title = $"{TitlePrefix} Child", ParentNoteId = grandparentId });

        var request = new UpdateNoteEndpoint.UpdateNoteRequest(
            $"{TitlePrefix} Updated", null, "Updated body", childId, null, null);

        var response = await _client.PutAsJsonAsync($"/api/notes/{grandparentId}", request, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemResponse>(JsonOptions);
        Assert.NotNull(problem);
        Assert.True(problem.Errors.ContainsKey("ParentNoteId"));
    }

    private sealed record ValidationProblemResponse(
        string? Type,
        string? Title,
        int? Status,
        Dictionary<string, string[]> Errors);
}
