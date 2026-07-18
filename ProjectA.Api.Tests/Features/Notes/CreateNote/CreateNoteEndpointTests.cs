using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ProjectA.Api.Data;
using ProjectA.Api.Features.Notes.CreateNote;
using Xunit;

namespace ProjectA.Api.Tests.Features.Notes.CreateNote;

[Collection(nameof(ApiCollection))]
public class CreateNoteEndpointTests : IAsyncLifetime
{
    private const string TitlePrefix = "Create Test Note";
    private const string LinkedBookmarkUrl = "https://example.com/create-test-note-bookmark";
    private const string LinkedAttachmentTitle = "Create Test Note Attachment";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _client;
    private readonly IDbConnectionFactory _connectionFactory;

    public CreateNoteEndpointTests(ApiFactory factory)
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

    [Fact]
    public async Task Post_WithValidRequest_CreatesNoteAndReturnsCreated()
    {
        var request = new CreateNoteEndpoint.CreateNoteRequest($"{TitlePrefix} New", "A short summary", "Some body text", null, false, null, null, null);

        var response = await _client.PostAsJsonAsync("/api/notes", request, JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var created = await response.Content.ReadFromJsonAsync<CreateNoteEndpoint.NoteResponse>(JsonOptions);
        Assert.NotNull(created);
        Assert.True(created.Id > 0);
        Assert.Contains($"/api/notes/{created.Id}", response.Headers.Location!.ToString());
        Assert.Equal($"{TitlePrefix} New", created.Title);
        Assert.Equal("A short summary", created.Description);
        Assert.Equal("Some body text", created.Body);
        Assert.Empty(created.BookmarkIds);
        Assert.Empty(created.AttachmentIds);
    }

    [Fact]
    public async Task Post_WithOnlyBody_Succeeds()
    {
        // Title is nullable in the schema; a body-only note should still be creatable.
        var request = new CreateNoteEndpoint.CreateNoteRequest(null, null, $"{TitlePrefix} body-only content", null, false, null, null, null);

        var response = await _client.PostAsJsonAsync("/api/notes", request, JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Post_WithNeitherTitleNorBody_ReturnsValidationProblem()
    {
        var request = new CreateNoteEndpoint.CreateNoteRequest(null, null, null, null, false, null, null, null);

        var response = await _client.PostAsJsonAsync("/api/notes", request, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemResponse>(JsonOptions);
        Assert.NotNull(problem);
        Assert.True(problem.Errors.ContainsKey("Title"));
        Assert.True(problem.Errors.ContainsKey("Body"));
    }

    [Fact]
    public async Task Post_WithValidBookmarkAndAttachmentIds_AssociatesThem()
    {
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
        }

        var request = new CreateNoteEndpoint.CreateNoteRequest(
            $"{TitlePrefix} Linked", null, null, null, false, [bookmarkId], [attachmentId], null);

        var response = await _client.PostAsJsonAsync("/api/notes", request, JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<CreateNoteEndpoint.NoteResponse>(JsonOptions);
        Assert.NotNull(created);
        Assert.Equal([bookmarkId], created.BookmarkIds);
        Assert.Equal([attachmentId], created.AttachmentIds);
    }

    [Fact]
    public async Task Post_WithBookmarkIdThatDoesNotExist_ReturnsValidationProblem()
    {
        var request = new CreateNoteEndpoint.CreateNoteRequest($"{TitlePrefix} BadBookmark", null, null, null, false, [999999], null, null);

        var response = await _client.PostAsJsonAsync("/api/notes", request, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemResponse>(JsonOptions);
        Assert.NotNull(problem);
        Assert.True(problem.Errors.ContainsKey("BookmarkIds"));
    }

    [Fact]
    public async Task Post_WithAttachmentIdThatDoesNotExist_ReturnsValidationProblem()
    {
        var request = new CreateNoteEndpoint.CreateNoteRequest($"{TitlePrefix} BadAttachment", null, null, null, false, null, [999999], null);

        var response = await _client.PostAsJsonAsync("/api/notes", request, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemResponse>(JsonOptions);
        Assert.NotNull(problem);
        Assert.True(problem.Errors.ContainsKey("AttachmentIds"));
    }

    [Fact]
    public async Task Post_WithParentNoteId_SetsParent()
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        var parentId = await connection.QuerySingleAsync<long>(
            "INSERT INTO notes (title) VALUES (@Title) RETURNING id;",
            new { Title = $"{TitlePrefix} Parent" });

        var request = new CreateNoteEndpoint.CreateNoteRequest($"{TitlePrefix} Child", null, null, parentId, false, null, null, null);

        var response = await _client.PostAsJsonAsync("/api/notes", request, JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<CreateNoteEndpoint.NoteResponse>(JsonOptions);
        Assert.NotNull(created);
        Assert.Equal(parentId, created.ParentNoteId);
    }

    [Fact]
    public async Task Post_WithParentNoteIdThatDoesNotExist_ReturnsValidationProblem()
    {
        var request = new CreateNoteEndpoint.CreateNoteRequest($"{TitlePrefix} BadParent", null, null, 999999, false, null, null, null);

        var response = await _client.PostAsJsonAsync("/api/notes", request, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemResponse>(JsonOptions);
        Assert.NotNull(problem);
        Assert.True(problem.Errors.ContainsKey("ParentNoteId"));
    }

    [Fact]
    public async Task Post_WithIsPrivateTrue_SetsPrivateFlag()
    {
        var request = new CreateNoteEndpoint.CreateNoteRequest($"{TitlePrefix} Private", null, null, null, true, null, null, null);

        var response = await _client.PostAsJsonAsync("/api/notes", request, JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<CreateNoteEndpoint.NoteResponse>(JsonOptions);
        Assert.NotNull(created);
        Assert.True(created.IsPrivate);
    }

    private sealed record ValidationProblemResponse(
        string? Type,
        string? Title,
        int? Status,
        Dictionary<string, string[]> Errors);
}
