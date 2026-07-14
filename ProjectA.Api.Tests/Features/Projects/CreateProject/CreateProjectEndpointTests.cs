using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ProjectA.Api.Data;
using ProjectA.Api.Features.Projects.CreateProject;
using ProjectA.Api.Features.Projects.GetProjectById;
using Xunit;

namespace ProjectA.Api.Tests.Features.Projects.CreateProject;

[Collection(nameof(ApiCollection))]
public class CreateProjectEndpointTests : IAsyncLifetime
{
    private const string TitlePrefix = "Create Test Project";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _client;
    private readonly IDbConnectionFactory _connectionFactory;

    public CreateProjectEndpointTests(ApiFactory factory)
    {
        _client = factory.CreateAuthenticatedClient();
        _connectionFactory = factory.Services.GetRequiredService<IDbConnectionFactory>();
    }

    public async Task InitializeAsync() => await CleanUpAsync();

    public async Task DisposeAsync() => await CleanUpAsync();

    private async Task CleanUpAsync()
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();

        // Join rows first - the FK constraint would otherwise stop the project/note/bookmark/
        // attachment rows themselves being removed.
        await connection.ExecuteAsync(
            "DELETE FROM project_notes WHERE " +
            "project_id IN (SELECT id FROM projects WHERE title LIKE @Pattern) OR " +
            "note_id IN (SELECT id FROM notes WHERE title LIKE @Pattern);",
            new { Pattern = $"{TitlePrefix}%" });
        await connection.ExecuteAsync(
            "DELETE FROM project_bookmarks WHERE " +
            "project_id IN (SELECT id FROM projects WHERE title LIKE @Pattern) OR " +
            "bookmark_id IN (SELECT id FROM bookmarks WHERE title LIKE @Pattern);",
            new { Pattern = $"{TitlePrefix}%" });
        await connection.ExecuteAsync(
            "DELETE FROM project_attachments WHERE " +
            "project_id IN (SELECT id FROM projects WHERE title LIKE @Pattern) OR " +
            "attachment_id IN (SELECT id FROM attachments WHERE title LIKE @Pattern);",
            new { Pattern = $"{TitlePrefix}%" });
        await connection.ExecuteAsync("DELETE FROM projects WHERE title LIKE @Pattern;", new { Pattern = $"{TitlePrefix}%" });
        await connection.ExecuteAsync("DELETE FROM notes WHERE title LIKE @Pattern;", new { Pattern = $"{TitlePrefix}%" });
        await connection.ExecuteAsync("DELETE FROM bookmarks WHERE title LIKE @Pattern;", new { Pattern = $"{TitlePrefix}%" });
        await connection.ExecuteAsync("DELETE FROM attachments WHERE title LIKE @Pattern;", new { Pattern = $"{TitlePrefix}%" });
    }

    private async Task<long> SeedNoteAsync(string suffix)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        return await connection.QuerySingleAsync<long>(
            "INSERT INTO notes (title) VALUES (@Title) RETURNING id;",
            new { Title = $"{TitlePrefix} {suffix}" });
    }

    private async Task<long> SeedBookmarkAsync(string suffix)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        return await connection.QuerySingleAsync<long>(
            "INSERT INTO bookmarks (url, title) VALUES ('https://example.com/x', @Title) RETURNING id;",
            new { Title = $"{TitlePrefix} {suffix}" });
    }

    private async Task<long> SeedAttachmentAsync(string suffix)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        return await connection.QuerySingleAsync<long>(
            "INSERT INTO attachments (title, s3_arn) VALUES (@Title, 'arn:aws:s3:::bucket/key') RETURNING id;",
            new { Title = $"{TitlePrefix} {suffix}" });
    }

    [Fact]
    public async Task Post_WithValidRequest_CreatesProjectAndReturnsCreated()
    {
        var request = new CreateProjectEndpoint.CreateProjectRequest(
            $"{TitlePrefix} New", "A description", new DateTime(2026, 6, 1).Date, null, null, null);

        var response = await _client.PostAsJsonAsync("/api/projects", request, JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var created = await response.Content.ReadFromJsonAsync<CreateProjectEndpoint.ProjectResponse>(JsonOptions);
        Assert.NotNull(created);
        Assert.True(created.Id > 0);
        Assert.Contains($"/api/projects/{created.Id}", response.Headers.Location!.ToString());
        Assert.Equal($"{TitlePrefix} New", created.Title);
        Assert.Equal(new DateTime(2026, 6, 1).Date, created.StartDate);
    }

    [Fact]
    public async Task Post_WithMissingTitle_ReturnsValidationProblem()
    {
        var request = new CreateProjectEndpoint.CreateProjectRequest(" ", null, new DateTime(2026, 6, 1).Date, null, null, null);

        var response = await _client.PostAsJsonAsync("/api/projects", request, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemResponse>(JsonOptions);
        Assert.NotNull(problem);
        Assert.True(problem.Errors.ContainsKey("Title"));
    }

    [Fact]
    public async Task Post_WithMissingStartDate_ReturnsValidationProblem()
    {
        var request = new CreateProjectEndpoint.CreateProjectRequest($"{TitlePrefix} NoDate", null, null, null, null, null);

        var response = await _client.PostAsJsonAsync("/api/projects", request, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemResponse>(JsonOptions);
        Assert.NotNull(problem);
        Assert.True(problem.Errors.ContainsKey("StartDate"));
    }

    [Fact]
    public async Task Post_WithAssociationIds_AssociatesThemAndReturnsThem()
    {
        var noteId = await SeedNoteAsync("Note");
        var bookmarkId = await SeedBookmarkAsync("Bookmark");
        var attachmentId = await SeedAttachmentAsync("Attachment");

        var request = new CreateProjectEndpoint.CreateProjectRequest(
            $"{TitlePrefix} Associated", null, new DateTime(2026, 6, 1).Date,
            [noteId], [bookmarkId], [attachmentId]);

        var response = await _client.PostAsJsonAsync("/api/projects", request, JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<CreateProjectEndpoint.ProjectResponse>(JsonOptions);
        Assert.NotNull(created);
        Assert.Equal([noteId], created.NoteIds);
        Assert.Equal([bookmarkId], created.BookmarkIds);
        Assert.Equal([attachmentId], created.AttachmentIds);

        var getResponse = await _client.GetAsync($"/api/projects/{created.Id}");
        var fetched = await getResponse.Content.ReadFromJsonAsync<GetProjectByIdEndpoint.ProjectResponse>(JsonOptions);
        Assert.NotNull(fetched);
        Assert.Equal([noteId], fetched.NoteIds);
        Assert.Equal([bookmarkId], fetched.BookmarkIds);
        Assert.Equal([attachmentId], fetched.AttachmentIds);
    }

    [Fact]
    public async Task Post_WithInvalidNoteId_ReturnsValidationProblem_AndCreatesNoProject()
    {
        var request = new CreateProjectEndpoint.CreateProjectRequest(
            $"{TitlePrefix} BadNote", null, new DateTime(2026, 6, 1).Date, [999999], null, null);

        var response = await _client.PostAsJsonAsync("/api/projects", request, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemResponse>(JsonOptions);
        Assert.NotNull(problem);
        Assert.True(problem.Errors.ContainsKey("NoteIds"));

        using var connection = await _connectionFactory.CreateConnectionAsync();
        var count = await connection.QuerySingleAsync<long>(
            "SELECT COUNT(*) FROM projects WHERE title = @Title;", new { Title = $"{TitlePrefix} BadNote" });
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task Post_WithInvalidBookmarkId_ReturnsValidationProblem()
    {
        var request = new CreateProjectEndpoint.CreateProjectRequest(
            $"{TitlePrefix} BadBookmark", null, new DateTime(2026, 6, 1).Date, null, [999999], null);

        var response = await _client.PostAsJsonAsync("/api/projects", request, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemResponse>(JsonOptions);
        Assert.NotNull(problem);
        Assert.True(problem.Errors.ContainsKey("BookmarkIds"));
    }

    [Fact]
    public async Task Post_WithInvalidAttachmentId_ReturnsValidationProblem()
    {
        var request = new CreateProjectEndpoint.CreateProjectRequest(
            $"{TitlePrefix} BadAttachment", null, new DateTime(2026, 6, 1).Date, null, null, [999999]);

        var response = await _client.PostAsJsonAsync("/api/projects", request, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemResponse>(JsonOptions);
        Assert.NotNull(problem);
        Assert.True(problem.Errors.ContainsKey("AttachmentIds"));
    }

    private sealed record ValidationProblemResponse(
        string? Type,
        string? Title,
        int? Status,
        Dictionary<string, string[]> Errors);
}
