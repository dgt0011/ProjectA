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
    private readonly HttpClient _anonymousClient;
    private readonly IDbConnectionFactory _connectionFactory;

    public ProjectEndpointsTests(ApiFactory factory)
    {
        _client = factory.CreateAuthenticatedClient();
        _anonymousClient = factory.CreateClient();
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
            "DELETE FROM projects WHERE title LIKE @Pattern;",
            new { Pattern = $"{TitlePrefix}%" });
        await connection.ExecuteAsync(
            "DELETE FROM notes WHERE title LIKE @Pattern;",
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
        Assert.Contains(seeded, p => p.Title == $"{TitlePrefix} A" && p.StartDate == new DateTime(2026, 1, 1).Date);
        Assert.Contains(seeded, p => p.Title == $"{TitlePrefix} B" && p.StartDate == new DateTime(2026, 2, 1).Date);
    }

    [Fact]
    public async Task GetById_WhenExists_ReturnsProject()
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        var id = await connection.QuerySingleAsync<long>(
            "INSERT INTO projects (title, start_date) VALUES (@Title, '2026-03-15 00:00:00+00') RETURNING id;",
            new { Title = $"{TitlePrefix} ById" });

        var response = await _client.GetAsync($"/api/projects/{id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var project = await response.Content.ReadFromJsonAsync<GetProjectByIdEndpoint.ProjectResponse>(JsonOptions);
        Assert.NotNull(project);
        Assert.Equal(id, project.Id);
        Assert.Equal(new DateTime(2026, 3, 15).Date, project.StartDate);
    }

    [Fact]
    public async Task GetById_WhenMissing_ReturnsProblemDetails()
    {
        var response = await _client.GetAsync("/api/projects/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task GetList_IncludesNoteAssociations()
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();

        var projectId = await connection.QuerySingleAsync<long>(
            "INSERT INTO projects (title, start_date) VALUES (@Title, '2026-01-01') RETURNING id;",
            new { Title = $"{TitlePrefix} ListAssociated" });
        var noteId = await connection.QuerySingleAsync<long>(
            "INSERT INTO notes (title) VALUES (@Title) RETURNING id;",
            new { Title = $"{TitlePrefix} ListNote" });
        await connection.ExecuteAsync(
            "INSERT INTO project_notes (project_id, note_id) VALUES (@ProjectId, @NoteId);",
            new { ProjectId = projectId, NoteId = noteId });

        var response = await _client.GetAsync("/api/projects");
        var projects = await response.Content
            .ReadFromJsonAsync<List<GetProjectListEndpoint.ProjectListItemResponse>>(JsonOptions);
        Assert.NotNull(projects);

        var found = projects.Single(p => p.Id == projectId);
        Assert.Equal([noteId], found.NoteIds);
        Assert.Empty(found.BookmarkIds);
        Assert.Empty(found.AttachmentIds);
    }

    [Fact]
    public async Task GetById_IncludesNoteAssociations()
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();

        var projectId = await connection.QuerySingleAsync<long>(
            "INSERT INTO projects (title, start_date) VALUES (@Title, '2026-01-01') RETURNING id;",
            new { Title = $"{TitlePrefix} ByIdAssociated" });
        var noteId = await connection.QuerySingleAsync<long>(
            "INSERT INTO notes (title) VALUES (@Title) RETURNING id;",
            new { Title = $"{TitlePrefix} ByIdNote" });
        await connection.ExecuteAsync(
            "INSERT INTO project_notes (project_id, note_id) VALUES (@ProjectId, @NoteId);",
            new { ProjectId = projectId, NoteId = noteId });

        var response = await _client.GetAsync($"/api/projects/{projectId}");
        var project = await response.Content.ReadFromJsonAsync<GetProjectByIdEndpoint.ProjectResponse>(JsonOptions);
        Assert.NotNull(project);
        Assert.Equal([noteId], project.NoteIds);
    }

    [Fact]
    public async Task GetList_AsAnonymous_ExcludesPrivateNoteAssociations()
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();

        var projectId = await connection.QuerySingleAsync<long>(
            "INSERT INTO projects (title, start_date) VALUES (@Title, '2026-01-01') RETURNING id;",
            new { Title = $"{TitlePrefix} ListPrivateAssociated" });
        var publicNoteId = await connection.QuerySingleAsync<long>(
            "INSERT INTO notes (title) VALUES (@Title) RETURNING id;",
            new { Title = $"{TitlePrefix} ListPublicNote" });
        var privateNoteId = await connection.QuerySingleAsync<long>(
            "INSERT INTO notes (title, is_private) VALUES (@Title, true) RETURNING id;",
            new { Title = $"{TitlePrefix} ListPrivateNote" });
        await connection.ExecuteAsync(
            "INSERT INTO project_notes (project_id, note_id) VALUES (@ProjectId, @PublicNoteId), (@ProjectId, @PrivateNoteId);",
            new { ProjectId = projectId, PublicNoteId = publicNoteId, PrivateNoteId = privateNoteId });

        var anonymousResponse = await _anonymousClient.GetAsync("/api/projects");
        var anonymousProjects = await anonymousResponse.Content
            .ReadFromJsonAsync<List<GetProjectListEndpoint.ProjectListItemResponse>>(JsonOptions);
        Assert.NotNull(anonymousProjects);
        var anonymousFound = anonymousProjects.Single(p => p.Id == projectId);
        Assert.Equal([publicNoteId], anonymousFound.NoteIds);

        var authenticatedResponse = await _client.GetAsync("/api/projects");
        var authenticatedProjects = await authenticatedResponse.Content
            .ReadFromJsonAsync<List<GetProjectListEndpoint.ProjectListItemResponse>>(JsonOptions);
        Assert.NotNull(authenticatedProjects);
        var authenticatedFound = authenticatedProjects.Single(p => p.Id == projectId);
        Assert.Equal(2, authenticatedFound.NoteIds.Count);
        Assert.Contains(privateNoteId, authenticatedFound.NoteIds);
    }

    [Fact]
    public async Task GetById_AsAnonymous_ExcludesPrivateNoteAssociations()
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();

        var projectId = await connection.QuerySingleAsync<long>(
            "INSERT INTO projects (title, start_date) VALUES (@Title, '2026-01-01') RETURNING id;",
            new { Title = $"{TitlePrefix} ByIdPrivateAssociated" });
        var publicNoteId = await connection.QuerySingleAsync<long>(
            "INSERT INTO notes (title) VALUES (@Title) RETURNING id;",
            new { Title = $"{TitlePrefix} ByIdPublicNote" });
        var privateNoteId = await connection.QuerySingleAsync<long>(
            "INSERT INTO notes (title, is_private) VALUES (@Title, true) RETURNING id;",
            new { Title = $"{TitlePrefix} ByIdPrivateNote" });
        await connection.ExecuteAsync(
            "INSERT INTO project_notes (project_id, note_id) VALUES (@ProjectId, @PublicNoteId), (@ProjectId, @PrivateNoteId);",
            new { ProjectId = projectId, PublicNoteId = publicNoteId, PrivateNoteId = privateNoteId });

        var anonymousResponse = await _anonymousClient.GetAsync($"/api/projects/{projectId}");
        var anonymousProject = await anonymousResponse.Content.ReadFromJsonAsync<GetProjectByIdEndpoint.ProjectResponse>(JsonOptions);
        Assert.NotNull(anonymousProject);
        Assert.Equal([publicNoteId], anonymousProject.NoteIds);

        var authenticatedResponse = await _client.GetAsync($"/api/projects/{projectId}");
        var authenticatedProject = await authenticatedResponse.Content.ReadFromJsonAsync<GetProjectByIdEndpoint.ProjectResponse>(JsonOptions);
        Assert.NotNull(authenticatedProject);
        Assert.Equal(2, authenticatedProject.NoteIds.Count);
        Assert.Contains(privateNoteId, authenticatedProject.NoteIds);
    }

    [Fact]
    public async Task GetList_AsAnonymous_ExcludesPrivateProjects()
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        var privateProjectId = await connection.QuerySingleAsync<long>(
            "INSERT INTO projects (title, start_date, is_private) VALUES (@Title, '2026-01-01', true) RETURNING id;",
            new { Title = $"{TitlePrefix} PrivateProject" });
        var publicProjectId = await connection.QuerySingleAsync<long>(
            "INSERT INTO projects (title, start_date, is_private) VALUES (@Title, '2026-01-01', false) RETURNING id;",
            new { Title = $"{TitlePrefix} PublicProject" });

        var anonymousResponse = await _anonymousClient.GetAsync("/api/projects");
        var anonymousProjects = await anonymousResponse.Content
            .ReadFromJsonAsync<List<GetProjectListEndpoint.ProjectListItemResponse>>(JsonOptions);
        Assert.NotNull(anonymousProjects);
        Assert.DoesNotContain(anonymousProjects, p => p.Id == privateProjectId);
        Assert.Contains(anonymousProjects, p => p.Id == publicProjectId);

        var authenticatedResponse = await _client.GetAsync("/api/projects");
        var authenticatedProjects = await authenticatedResponse.Content
            .ReadFromJsonAsync<List<GetProjectListEndpoint.ProjectListItemResponse>>(JsonOptions);
        Assert.NotNull(authenticatedProjects);
        Assert.Contains(authenticatedProjects, p => p.Id == privateProjectId);
        Assert.Contains(authenticatedProjects, p => p.Id == publicProjectId);
    }

    [Fact]
    public async Task GetById_AsAnonymous_OnPrivateProject_ReturnsNotFound()
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        var privateProjectId = await connection.QuerySingleAsync<long>(
            "INSERT INTO projects (title, start_date, is_private) VALUES (@Title, '2026-01-01', true) RETURNING id;",
            new { Title = $"{TitlePrefix} PrivateById" });

        var anonymousResponse = await _anonymousClient.GetAsync($"/api/projects/{privateProjectId}");
        Assert.Equal(HttpStatusCode.NotFound, anonymousResponse.StatusCode);

        var authenticatedResponse = await _client.GetAsync($"/api/projects/{privateProjectId}");
        Assert.Equal(HttpStatusCode.OK, authenticatedResponse.StatusCode);
    }
}
