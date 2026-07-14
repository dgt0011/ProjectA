using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ProjectA.Api.Data;
using ProjectA.Api.Features.Notes.GetNoteById;
using ProjectA.Api.Features.Notes.GetNoteList;
using Xunit;

namespace ProjectA.Api.Tests.Features.Notes;

[Collection(nameof(ApiCollection))]
public class NoteEndpointsTests : IAsyncLifetime
{
    private const string TitlePrefix = "List Test Note";
    private const string LinkedBookmarkUrl = "https://example.com/list-test-note-bookmark";
    private const string LinkedAttachmentTitle = "List Test Note Attachment";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _client;
    private readonly IDbConnectionFactory _connectionFactory;

    public NoteEndpointsTests(ApiFactory factory)
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
    public async Task GetList_ReturnsAllNotes()
    {
        using (var connection = await _connectionFactory.CreateConnectionAsync())
        {
            await connection.ExecuteAsync(
                "INSERT INTO notes (title, description, body) VALUES (@TitleA, 'Summary A', 'Body A'), (@TitleB, NULL, NULL);",
                new { TitleA = $"{TitlePrefix} A", TitleB = $"{TitlePrefix} B" });
        }

        var response = await _client.GetAsync("/api/notes");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var notes = await response.Content.ReadFromJsonAsync<List<GetNoteListEndpoint.NoteListItemResponse>>(JsonOptions);
        Assert.NotNull(notes);

        var seeded = notes.Where(n => n.Title != null && n.Title.StartsWith(TitlePrefix)).ToList();
        Assert.Equal(2, seeded.Count);
        Assert.Contains(seeded, n => n.Title == $"{TitlePrefix} A" && n.Description == "Summary A" && n.Body == "Body A");
        Assert.Contains(seeded, n => n.Title == $"{TitlePrefix} B" && n.Description == null && n.Body == null);
    }

    [Fact]
    public async Task GetById_WhenExists_ReturnsNote()
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        var id = await connection.QuerySingleAsync<long>(
            "INSERT INTO notes (title, description, body) VALUES (@Title, 'A summary', 'Some body') RETURNING id;",
            new { Title = $"{TitlePrefix} ById" });

        var response = await _client.GetAsync($"/api/notes/{id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var note = await response.Content.ReadFromJsonAsync<GetNoteByIdEndpoint.NoteResponse>(JsonOptions);
        Assert.NotNull(note);
        Assert.Equal(id, note.Id);
        Assert.Equal($"{TitlePrefix} ById", note.Title);
        Assert.Equal("A summary", note.Description);
        Assert.Equal("Some body", note.Body);
    }

    [Fact]
    public async Task GetById_WhenMissing_ReturnsProblemDetails()
    {
        var response = await _client.GetAsync("/api/notes/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task GetById_IncludesBookmarkAndAttachmentIds()
    {
        long noteId;
        long bookmarkId;
        long attachmentId;
        using (var connection = await _connectionFactory.CreateConnectionAsync())
        {
            noteId = await connection.QuerySingleAsync<long>(
                "INSERT INTO notes (title) VALUES (@Title) RETURNING id;",
                new { Title = $"{TitlePrefix} Linked" });
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

        var response = await _client.GetAsync($"/api/notes/{noteId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var note = await response.Content.ReadFromJsonAsync<GetNoteByIdEndpoint.NoteResponse>(JsonOptions);
        Assert.NotNull(note);
        Assert.Equal([bookmarkId], note.BookmarkIds);
        Assert.Equal([attachmentId], note.AttachmentIds);

        var listResponse = await _client.GetAsync("/api/notes");
        var notes = await listResponse.Content.ReadFromJsonAsync<List<GetNoteListEndpoint.NoteListItemResponse>>(JsonOptions);
        var listItem = notes!.Single(n => n.Id == noteId);
        Assert.Equal([bookmarkId], listItem.BookmarkIds);
        Assert.Equal([attachmentId], listItem.AttachmentIds);
    }
}
