using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ProjectA.Api.Data;
using ProjectA.Api.Features.ToDo.UpdateToDo;
using Xunit;

namespace ProjectA.Api.Tests.Features.ToDo.UpdateToDo;

[Collection(nameof(ApiCollection))]
public class UpdateToDoEndpointTests : IAsyncLifetime
{
    private const string TestCategoryTitle = "Update Test Category";
    private const string TestProjectTitle = "Update Test Project";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _client;
    private readonly IDbConnectionFactory _connectionFactory;
    private long _categoryId;
    private long _projectId;

    public UpdateToDoEndpointTests(ApiFactory factory)
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
            "OR project_id IN (SELECT id FROM projects WHERE title = @ProjectTitle);",
            new { CategoryTitle = TestCategoryTitle, ProjectTitle = TestProjectTitle });
        await connection.ExecuteAsync("DELETE FROM categories WHERE title = @Title;", new { Title = TestCategoryTitle });
        await connection.ExecuteAsync("DELETE FROM projects WHERE title = @Title;", new { Title = TestProjectTitle });
    }

    private async Task<long> SeedToDoAsync(bool actioned = false)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        return await connection.QuerySingleAsync<long>(
            "INSERT INTO todos (title, actioned, category_id, description, date_created) " +
            "VALUES ('Original Title', @Actioned, @CategoryId, 'Original description', now()) " +
            "RETURNING id;",
            new { Actioned = actioned, CategoryId = _categoryId });
    }

    [Fact]
    public async Task Put_WithValidRequest_UpdatesAndReturnsOk()
    {
        var id = await SeedToDoAsync();
        var request = new UpdateToDoEndpoint.UpdateToDoRequest("Updated Title", _categoryId, "Updated description", true);

        var response = await _client.PutAsJsonAsync($"/api/todo/{id}", request, JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await response.Content.ReadFromJsonAsync<UpdateToDoEndpoint.ToDoResponse>(JsonOptions);
        Assert.NotNull(updated);
        Assert.Equal(id, updated.Id);
        Assert.Equal("Updated Title", updated.Title);
        Assert.Equal(_categoryId, updated.CategoryId);
        Assert.Equal("Updated description", updated.Description);
        Assert.True(updated.Done);
        Assert.NotNull(updated.DateModified);
    }

    [Fact]
    public async Task Put_WhenIdDoesNotExist_ReturnsProblemDetails()
    {
        var request = new UpdateToDoEndpoint.UpdateToDoRequest("Doesn't matter", _categoryId, null, false);

        var response = await _client.PutAsJsonAsync("/api/todo/999999", request, JsonOptions);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Put_WithMissingTitle_ReturnsValidationProblem()
    {
        var id = await SeedToDoAsync();
        var request = new UpdateToDoEndpoint.UpdateToDoRequest(" ", _categoryId, null, false);

        var response = await _client.PutAsJsonAsync($"/api/todo/{id}", request, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Put_WithoutCategory_SetsCategoryToNull()
    {
        // CategoryId is optional on update too - clearing it moves the ToDo into the
        // "Uncategorized" group wherever ToDo lists are displayed, rather than being rejected.
        var id = await SeedToDoAsync();
        var request = new UpdateToDoEndpoint.UpdateToDoRequest("Original Title", null, "Original description", false);

        var response = await _client.PutAsJsonAsync($"/api/todo/{id}", request, JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await response.Content.ReadFromJsonAsync<UpdateToDoEndpoint.ToDoResponse>(JsonOptions);
        Assert.NotNull(updated);
        Assert.Null(updated.CategoryId);
    }

    [Fact]
    public async Task Put_WithCategoryIdThatDoesNotExist_ReturnsValidationProblem()
    {
        var id = await SeedToDoAsync();
        var request = new UpdateToDoEndpoint.UpdateToDoRequest("Original Title", 999999, null, false);

        var response = await _client.PutAsJsonAsync($"/api/todo/{id}", request, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemResponse>(JsonOptions);
        Assert.NotNull(problem);
        Assert.True(problem.Errors.ContainsKey("CategoryId"));
    }

    [Fact]
    public async Task Put_CanMarkToDoAsDone()
    {
        var id = await SeedToDoAsync(actioned: false);
        var request = new UpdateToDoEndpoint.UpdateToDoRequest("Original Title", _categoryId, "Original description", true);

        var response = await _client.PutAsJsonAsync($"/api/todo/{id}", request, JsonOptions);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var getResponse = await _client.GetAsync($"/api/todo/{id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
    }

    [Fact]
    public async Task Put_WithProjectId_AssociatesToDoWithProject()
    {
        var id = await SeedToDoAsync();
        var request = new UpdateToDoEndpoint.UpdateToDoRequest("Original Title", _categoryId, "Original description", false, _projectId);

        var response = await _client.PutAsJsonAsync($"/api/todo/{id}", request, JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await response.Content.ReadFromJsonAsync<UpdateToDoEndpoint.ToDoResponse>(JsonOptions);
        Assert.NotNull(updated);
        Assert.Equal(_projectId, updated.ProjectId);
    }

    [Fact]
    public async Task Put_WithProjectIdThatDoesNotExist_ReturnsValidationProblem()
    {
        var id = await SeedToDoAsync();
        var request = new UpdateToDoEndpoint.UpdateToDoRequest("Original Title", _categoryId, null, false, 999999);

        var response = await _client.PutAsJsonAsync($"/api/todo/{id}", request, JsonOptions);

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
