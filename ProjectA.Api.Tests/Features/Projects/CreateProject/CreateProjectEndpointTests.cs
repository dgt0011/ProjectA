using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ProjectA.Api.Data;
using ProjectA.Api.Features.Projects.CreateProject;
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
        await connection.ExecuteAsync(
            "DELETE FROM projects WHERE title LIKE @Pattern;",
            new { Pattern = $"{TitlePrefix}%" });
    }

    [Fact]
    public async Task Post_WithValidRequest_CreatesProjectAndReturnsCreated()
    {
        var request = new CreateProjectEndpoint.CreateProjectRequest(
            $"{TitlePrefix} New", "A description", new DateTime(2026, 6, 1).Date);

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
        var request = new CreateProjectEndpoint.CreateProjectRequest(" ", null, new DateTime(2026, 6, 1).Date);

        var response = await _client.PostAsJsonAsync("/api/projects", request, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemResponse>(JsonOptions);
        Assert.NotNull(problem);
        Assert.True(problem.Errors.ContainsKey("Title"));
    }

    [Fact]
    public async Task Post_WithMissingStartDate_ReturnsValidationProblem()
    {
        var request = new CreateProjectEndpoint.CreateProjectRequest($"{TitlePrefix} NoDate", null, null);

        var response = await _client.PostAsJsonAsync("/api/projects", request, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemResponse>(JsonOptions);
        Assert.NotNull(problem);
        Assert.True(problem.Errors.ContainsKey("StartDate"));
    }

    private sealed record ValidationProblemResponse(
        string? Type,
        string? Title,
        int? Status,
        Dictionary<string, string[]> Errors);
}
