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
        // work around would stop the note/project/category rows themselves being removed.
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
        await connection.ExecuteAsync("DELETE FROM projects WHERE title LIKE @Pattern;", new { Pattern = $"{TitlePrefix}%" });
        await connection.ExecuteAsync("DELETE FROM notes WHERE title LIKE @Pattern;", new { Pattern = $"{TitlePrefix}%" });
        await connection.ExecuteAsync("DELETE FROM categories WHERE title LIKE @Pattern;", new { Pattern = $"{TitlePrefix}%" });
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
}
