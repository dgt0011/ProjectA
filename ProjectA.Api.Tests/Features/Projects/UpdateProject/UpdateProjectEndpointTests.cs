using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ProjectA.Api.Data;
using ProjectA.Api.Features.Projects.UpdateProject;
using Xunit;

namespace ProjectA.Api.Tests.Features.Projects.UpdateProject;

[Collection(nameof(ApiCollection))]
public class UpdateProjectEndpointTests : IAsyncLifetime
{
    private const string TitlePrefix = "Update Test Project";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _client;
    private readonly IDbConnectionFactory _connectionFactory;

    public UpdateProjectEndpointTests(ApiFactory factory)
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

    private async Task<long> SeedProjectAsync()
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        return await connection.QuerySingleAsync<long>(
            "INSERT INTO projects (title, description, start_date) " +
            "VALUES (@Title, 'Original description', '2026-01-01') RETURNING id;",
            new { Title = $"{TitlePrefix} Original" });
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

    private async Task LinkNoteAsync(long projectId, long noteId)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        await connection.ExecuteAsync(
            "INSERT INTO project_notes (project_id, note_id) VALUES (@ProjectId, @NoteId);",
            new { ProjectId = projectId, NoteId = noteId });
    }

    [Fact]
    public async Task Put_WithValidRequest_UpdatesAndReturnsOk()
    {
        var id = await SeedProjectAsync();
        var request = new UpdateProjectEndpoint.UpdateProjectRequest(
            $"{TitlePrefix} Updated", "Updated description", new DateTime(2026, 7, 4).Date, false, null, null, null);

        var response = await _client.PutAsJsonAsync($"/api/projects/{id}", request, JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await response.Content.ReadFromJsonAsync<UpdateProjectEndpoint.ProjectResponse>(JsonOptions);
        Assert.NotNull(updated);
        Assert.Equal(id, updated.Id);
        Assert.Equal($"{TitlePrefix} Updated", updated.Title);
        Assert.Equal(new DateTime(2026, 7, 4).Date, updated.StartDate);
    }

    [Fact]
    public async Task Put_WhenIdDoesNotExist_ReturnsProblemDetails()
    {
        var request = new UpdateProjectEndpoint.UpdateProjectRequest(
            $"{TitlePrefix} Missing", null, new DateTime(2026, 1, 1).Date, false, null, null, null);

        var response = await _client.PutAsJsonAsync("/api/projects/999999", request, JsonOptions);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Put_WithMissingTitle_ReturnsValidationProblem()
    {
        var id = await SeedProjectAsync();
        var request = new UpdateProjectEndpoint.UpdateProjectRequest(" ", null, new DateTime(2026, 1, 1).Date, false, null, null, null);

        var response = await _client.PutAsJsonAsync($"/api/projects/{id}", request, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Put_WithNoteIds_ReplacesExistingAssociations()
    {
        var projectId = await SeedProjectAsync();
        var oldNoteId = await SeedNoteAsync("Old");
        var newNoteId = await SeedNoteAsync("New");
        await LinkNoteAsync(projectId, oldNoteId);

        var request = new UpdateProjectEndpoint.UpdateProjectRequest(
            $"{TitlePrefix} Original", null, new DateTime(2026, 1, 1).Date, false, [newNoteId], null, null);

        var response = await _client.PutAsJsonAsync($"/api/projects/{projectId}", request, JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await response.Content.ReadFromJsonAsync<UpdateProjectEndpoint.ProjectResponse>(JsonOptions);
        Assert.NotNull(updated);
        Assert.Equal([newNoteId], updated.NoteIds);
    }

    [Fact]
    public async Task Put_WithoutAssociationIds_LeavesExistingAssociationsUnchanged()
    {
        var projectId = await SeedProjectAsync();
        var noteId = await SeedNoteAsync("Untouched");
        await LinkNoteAsync(projectId, noteId);

        var request = new UpdateProjectEndpoint.UpdateProjectRequest(
            $"{TitlePrefix} Original", null, new DateTime(2026, 1, 1).Date, false, null, null, null);

        var response = await _client.PutAsJsonAsync($"/api/projects/{projectId}", request, JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await response.Content.ReadFromJsonAsync<UpdateProjectEndpoint.ProjectResponse>(JsonOptions);
        Assert.NotNull(updated);
        Assert.Equal([noteId], updated.NoteIds);
    }

    [Fact]
    public async Task Put_WithEmptyNoteIds_ClearsExistingAssociations()
    {
        var projectId = await SeedProjectAsync();
        var noteId = await SeedNoteAsync("ToRemove");
        await LinkNoteAsync(projectId, noteId);

        var request = new UpdateProjectEndpoint.UpdateProjectRequest(
            $"{TitlePrefix} Original", null, new DateTime(2026, 1, 1).Date, false, [], null, null);

        var response = await _client.PutAsJsonAsync($"/api/projects/{projectId}", request, JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await response.Content.ReadFromJsonAsync<UpdateProjectEndpoint.ProjectResponse>(JsonOptions);
        Assert.NotNull(updated);
        Assert.Empty(updated.NoteIds);
    }

    [Fact]
    public async Task Put_WithInvalidBookmarkId_ReturnsValidationProblem_AndLeavesProjectUnchanged()
    {
        var projectId = await SeedProjectAsync();
        var request = new UpdateProjectEndpoint.UpdateProjectRequest(
            $"{TitlePrefix} ShouldNotApply", null, new DateTime(2026, 1, 1).Date, false, null, [999999], null);

        var response = await _client.PutAsJsonAsync($"/api/projects/{projectId}", request, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemResponse>(JsonOptions);
        Assert.NotNull(problem);
        Assert.True(problem.Errors.ContainsKey("BookmarkIds"));

        using var connection = await _connectionFactory.CreateConnectionAsync();
        var title = await connection.QuerySingleAsync<string>(
            "SELECT title FROM projects WHERE id = @Id;", new { Id = projectId });
        Assert.Equal($"{TitlePrefix} Original", title);
    }

    [Fact]
    public async Task Put_WithIsPrivateTrue_SetsPrivateFlag()
    {
        var id = await SeedProjectAsync();
        var request = new UpdateProjectEndpoint.UpdateProjectRequest(
            $"{TitlePrefix} Original", null, new DateTime(2026, 1, 1).Date, true, null, null, null);

        var response = await _client.PutAsJsonAsync($"/api/projects/{id}", request, JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await response.Content.ReadFromJsonAsync<UpdateProjectEndpoint.ProjectResponse>(JsonOptions);
        Assert.NotNull(updated);
        Assert.True(updated.IsPrivate);
    }

    private sealed record ValidationProblemResponse(
        string? Type,
        string? Title,
        int? Status,
        Dictionary<string, string[]> Errors);
}
