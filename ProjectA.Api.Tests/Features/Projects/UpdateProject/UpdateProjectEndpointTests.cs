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
            "DELETE FROM projects WHERE title LIKE @Pattern;",
            new { Pattern = $"{TitlePrefix}%" });
    }

    private async Task<long> SeedProjectAsync()
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        return await connection.QuerySingleAsync<long>(
            "INSERT INTO projects (title, description, start_date) " +
            "VALUES (@Title, 'Original description', '2026-01-01') RETURNING id;",
            new { Title = $"{TitlePrefix} Original" });
    }

    [Fact]
    public async Task Put_WithValidRequest_UpdatesAndReturnsOk()
    {
        var id = await SeedProjectAsync();
        var request = new UpdateProjectEndpoint.UpdateProjectRequest(
            $"{TitlePrefix} Updated", "Updated description", new DateTime(2026, 7, 4).Date);

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
            $"{TitlePrefix} Missing", null, new DateTime(2026, 1, 1).Date);

        var response = await _client.PutAsJsonAsync("/api/projects/999999", request, JsonOptions);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Put_WithMissingTitle_ReturnsValidationProblem()
    {
        var id = await SeedProjectAsync();
        var request = new UpdateProjectEndpoint.UpdateProjectRequest(" ", null, new DateTime(2026, 1, 1).Date);

        var response = await _client.PutAsJsonAsync($"/api/projects/{id}", request, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
