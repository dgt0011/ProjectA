using System.Net;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ProjectA.Api.Data;
using Xunit;

namespace ProjectA.Api.Tests.Features.Projects.DeleteProject;

[Collection(nameof(ApiCollection))]
public class DeleteProjectEndpointTests : IAsyncLifetime
{
    private const string TitlePrefix = "Delete Test Project";

    private readonly HttpClient _client;
    private readonly IDbConnectionFactory _connectionFactory;

    public DeleteProjectEndpointTests(ApiFactory factory)
    {
        _client = factory.CreateClient();
        _connectionFactory = factory.Services.GetRequiredService<IDbConnectionFactory>();
    }

    public async Task InitializeAsync() => await CleanUpAsync();

    public async Task DisposeAsync() => await CleanUpAsync();

    private async Task CleanUpAsync()
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();

        // Delete any blocking todos first - otherwise the FK constraint this cleanup exists
        // to work around would stop the project rows themselves being removed.
        await connection.ExecuteAsync(
            "DELETE FROM todos WHERE project_id IN (SELECT id FROM projects WHERE title LIKE @Pattern);",
            new { Pattern = $"{TitlePrefix}%" });
        await connection.ExecuteAsync("DELETE FROM projects WHERE title LIKE @Pattern;", new { Pattern = $"{TitlePrefix}%" });
    }

    private async Task<long> SeedProjectAsync(string suffix)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        return await connection.QuerySingleAsync<long>(
            "INSERT INTO projects (title, start_date) VALUES (@Title, CURRENT_DATE) RETURNING id;",
            new { Title = $"{TitlePrefix} {suffix}" });
    }

    [Fact]
    public async Task Delete_WhenExists_RemovesProjectAndReturnsNoContent()
    {
        var id = await SeedProjectAsync("ToDelete");

        var response = await _client.DeleteAsync($"/api/projects/{id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var followUp = await _client.GetAsync($"/api/projects/{id}");
        Assert.Equal(HttpStatusCode.NotFound, followUp.StatusCode);
    }

    [Fact]
    public async Task Delete_WhenMissing_ReturnsProblemDetails()
    {
        var response = await _client.DeleteAsync("/api/projects/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Delete_WhenReferencedByToDo_ReturnsConflict()
    {
        var id = await SeedProjectAsync("InUse");

        using (var connection = await _connectionFactory.CreateConnectionAsync())
        {
            await connection.ExecuteAsync(
                "INSERT INTO todos (title, actioned, category, project_id) " +
                "VALUES ('Blocking ToDo', false, 'Some Category', @ProjectId);",
                new { ProjectId = id });
        }

        var response = await _client.DeleteAsync($"/api/projects/{id}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}
