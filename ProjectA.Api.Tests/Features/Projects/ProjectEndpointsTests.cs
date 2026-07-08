using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ProjectA.Api.Data;
using ProjectA.Api.Features.Projects.GetProjectById;
using ProjectA.Api.Features.Projects.GetProjectList;
using Xunit;

namespace ProjectA.Api.Tests.Features.Projects;

[Collection(nameof(ApiCollection))]
public class ProjectEndpointsTests : IAsyncLifetime
{
    private const string TitlePrefix = "List Test Project";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _client;
    private readonly IDbConnectionFactory _connectionFactory;

    public ProjectEndpointsTests(ApiFactory factory)
    {
        _client = factory.CreateClient();
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
    public async Task GetList_ReturnsAllProjects()
    {
        using (var connection = await _connectionFactory.CreateConnectionAsync())
        {
            await connection.ExecuteAsync(
                "INSERT INTO projects (title, description, start_date) VALUES " +
                "(@TitleA, 'Description A', '2026-01-01'), (@TitleB, NULL, '2026-02-01');",
                new { TitleA = $"{TitlePrefix} A", TitleB = $"{TitlePrefix} B" });
        }

        var response = await _client.GetAsync("/api/projects");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var projects = await response.Content
            .ReadFromJsonAsync<List<GetProjectListEndpoint.ProjectListItemResponse>>(JsonOptions);
        Assert.NotNull(projects);

        var seeded = projects.Where(p => p.Title.StartsWith(TitlePrefix)).ToList();
        Assert.Equal(2, seeded.Count);
        Assert.Contains(seeded, p => p.Title == $"{TitlePrefix} A" && p.StartDate == new DateOnly(2026, 1, 1));
        Assert.Contains(seeded, p => p.Title == $"{TitlePrefix} B" && p.StartDate == new DateOnly(2026, 2, 1));
    }

    [Fact]
    public async Task GetById_WhenExists_ReturnsProject()
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        var id = await connection.QuerySingleAsync<long>(
            "INSERT INTO projects (title, start_date) VALUES (@Title, '2026-03-15') RETURNING id;",
            new { Title = $"{TitlePrefix} ById" });

        var response = await _client.GetAsync($"/api/projects/{id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var project = await response.Content.ReadFromJsonAsync<GetProjectByIdEndpoint.ProjectResponse>(JsonOptions);
        Assert.NotNull(project);
        Assert.Equal(id, project.Id);
        Assert.Equal(new DateOnly(2026, 3, 15), project.StartDate);
    }

    [Fact]
    public async Task GetById_WhenMissing_ReturnsProblemDetails()
    {
        var response = await _client.GetAsync("/api/projects/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}
