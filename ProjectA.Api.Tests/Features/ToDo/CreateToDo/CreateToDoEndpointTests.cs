using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ProjectA.Api.Data;
using ProjectA.Api.Features.ToDo.CreateToDo;
using Xunit;

namespace ProjectA.Api.Tests.Features.ToDo.CreateToDo;

[Collection(nameof(ApiCollection))]
public class CreateToDoEndpointTests : IAsyncLifetime
{
    private const string TestCategoryTitle = "Create Test Category";
    private const string TestProjectTitle = "Create Test Project";
    private const string UncategorizedToDoTitle = "Create Test Uncategorized ToDo";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _client;
    private readonly IDbConnectionFactory _connectionFactory;
    private long _categoryId;
    private long _projectId;

    public CreateToDoEndpointTests(ApiFactory factory)
    {
        _client = factory.CreateAuthenticatedClient();
        _connectionFactory = factory.Services.GetRequiredService<IDbConnectionFactory>();
    }

    public async Task InitializeAsync()
    {
        await CleanUpAsync();

        using var connection = await _connectionFactory.CreateConnectionAsync();
        _categoryId = await connection.QuerySingleAsync<long>(
            "INSERT INTO categories (title) VALUES (@Title) RETURNING id;",
            new { Title = TestCategoryTitle });
        _projectId = await connection.QuerySingleAsync<long>(
            "INSERT INTO projects (title, start_date) VALUES (@Title, '2026-01-01') RETURNING id;",
            new { Title = TestProjectTitle });
    }

    public async Task DisposeAsync() => await CleanUpAsync();

    private async Task CleanUpAsync()
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        await connection.ExecuteAsync(
            "DELETE FROM todos WHERE category_id IN (SELECT id FROM categories WHERE title = @CategoryTitle) " +
            "OR project_id IN (SELECT id FROM projects WHERE title = @ProjectTitle) " +
            "OR title = @UncategorizedToDoTitle;",
            new { CategoryTitle = TestCategoryTitle, ProjectTitle = TestProjectTitle, UncategorizedToDoTitle });
        await connection.ExecuteAsync("DELETE FROM categories WHERE title = @Title;", new { Title = TestCategoryTitle });
        await connection.ExecuteAsync("DELETE FROM projects WHERE title = @Title;", new { Title = TestProjectTitle });
    }

    [Fact]
    public async Task Post_WithValidRequest_CreatesToDoAndReturnsCreated()
    {
        var request = new CreateToDoEndpoint.CreateToDoRequest("New ToDo", _categoryId, "A description");

        var response = await _client.PostAsJsonAsync("/api/todo", request, JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var created = await response.Content.ReadFromJsonAsync<CreateToDoEndpoint.ToDoResponse>(JsonOptions);
        Assert.NotNull(created);
        Assert.True(created.Id > 0);
        Assert.Contains($"/api/todo/{created.Id}", response.Headers.Location!.ToString());
        Assert.Equal("New ToDo", created.Title);
        Assert.Equal(_categoryId, created.CategoryId);
        Assert.Equal("A description", created.Description);
        Assert.False(created.Done);
        Assert.NotNull(created.DateCreated);
        Assert.Null(created.DateModified);
    }

    [Fact]
    public async Task Post_WithMissingTitle_ReturnsValidationProblem()
    {
        var request = new CreateToDoEndpoint.CreateToDoRequest("", _categoryId, null);

        var response = await _client.PostAsJsonAsync("/api/todo", request, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemResponse>(JsonOptions);
        Assert.NotNull(problem);
        Assert.True(problem.Errors.ContainsKey("Title"));
    }

    [Fact]
    public async Task Post_WithoutCategory_CreatesToDoWithNullCategory()
    {
        // CategoryId is optional - a ToDo without one is grouped as "Uncategorized" wherever
        // ToDo lists are displayed, rather than being rejected.
        var request = new CreateToDoEndpoint.CreateToDoRequest(UncategorizedToDoTitle, null, null);

        var response = await _client.PostAsJsonAsync("/api/todo", request, JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<CreateToDoEndpoint.ToDoResponse>(JsonOptions);
        Assert.NotNull(created);
        Assert.Null(created.CategoryId);
        Assert.Equal(UncategorizedToDoTitle, created.Title);
    }

    [Fact]
    public async Task Post_WithCategoryIdThatDoesNotExist_ReturnsValidationProblem()
    {
        var request = new CreateToDoEndpoint.CreateToDoRequest("A title", 999999, null);

        var response = await _client.PostAsJsonAsync("/api/todo", request, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemResponse>(JsonOptions);
        Assert.NotNull(problem);
        Assert.True(problem.Errors.ContainsKey("CategoryId"));
    }

    [Fact]
    public async Task Post_WithProjectId_CreatesToDoAssociatedWithProject()
    {
        var request = new CreateToDoEndpoint.CreateToDoRequest("Project ToDo", _categoryId, null, _projectId);

        var response = await _client.PostAsJsonAsync("/api/todo", request, JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<CreateToDoEndpoint.ToDoResponse>(JsonOptions);
        Assert.NotNull(created);
        Assert.Equal(_projectId, created.ProjectId);
    }

    [Fact]
    public async Task Post_WithoutProjectId_LeavesProjectIdNull()
    {
        var request = new CreateToDoEndpoint.CreateToDoRequest("No project ToDo", _categoryId, null);

        var response = await _client.PostAsJsonAsync("/api/todo", request, JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<CreateToDoEndpoint.ToDoResponse>(JsonOptions);
        Assert.NotNull(created);
        Assert.Null(created.ProjectId);
    }

    [Fact]
    public async Task Post_WithProjectIdThatDoesNotExist_ReturnsValidationProblem()
    {
        var request = new CreateToDoEndpoint.CreateToDoRequest("A title", _categoryId, null, 999999);

        var response = await _client.PostAsJsonAsync("/api/todo", request, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemResponse>(JsonOptions);
        Assert.NotNull(problem);
        Assert.True(problem.Errors.ContainsKey("ProjectId"));
    }

    private sealed record ValidationProblemResponse(
        string? Type,
        string? Title,
        int? Status,
        Dictionary<string, string[]> Errors);
}
