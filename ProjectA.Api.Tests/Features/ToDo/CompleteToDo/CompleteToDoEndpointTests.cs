using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ProjectA.Api.Data;
using ProjectA.Api.Features.ToDo.CompleteToDo;
using Xunit;

namespace ProjectA.Api.Tests.Features.ToDo.CompleteToDo;

[Collection(nameof(ApiCollection))]
public class CompleteToDoEndpointTests : IAsyncLifetime
{
    private const string TestCategoryTitle = "Complete Test Category";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _client;
    private readonly IDbConnectionFactory _connectionFactory;
    private long _categoryId;

    public CompleteToDoEndpointTests(ApiFactory factory)
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
    }

    public async Task DisposeAsync() => await CleanUpAsync();

    private async Task CleanUpAsync()
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        await connection.ExecuteAsync(
            "DELETE FROM todos WHERE category_id IN (SELECT id FROM categories WHERE title = @Title);",
            new { Title = TestCategoryTitle });
        await connection.ExecuteAsync("DELETE FROM categories WHERE title = @Title;", new { Title = TestCategoryTitle });
    }

    private async Task<long> SeedToDoAsync()
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        return await connection.QuerySingleAsync<long>(
            "INSERT INTO todos (title, actioned, category_id, description, date_created) " +
            "VALUES ('Outstanding ToDo', false, @CategoryId, 'Original description', now()) " +
            "RETURNING id;",
            new { CategoryId = _categoryId });
    }

    [Fact]
    public async Task Put_WithNotes_MarksDoneAndStoresCompletionNotes()
    {
        var id = await SeedToDoAsync();
        var request = new CompleteToDoEndpoint.CompleteToDoRequest("Finished via **Markdown** notes.");

        var response = await _client.PutAsJsonAsync($"/api/todo/{id}/complete", request, JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var completed = await response.Content.ReadFromJsonAsync<CompleteToDoEndpoint.ToDoResponse>(JsonOptions);
        Assert.NotNull(completed);
        Assert.True(completed.Done);
        Assert.Equal("Finished via **Markdown** notes.", completed.CompletionNotes);
        Assert.NotNull(completed.DateModified);
    }

    [Fact]
    public async Task Put_WithoutNotes_MarksDoneWithNullCompletionNotes()
    {
        var id = await SeedToDoAsync();
        var request = new CompleteToDoEndpoint.CompleteToDoRequest(null);

        var response = await _client.PutAsJsonAsync($"/api/todo/{id}/complete", request, JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var completed = await response.Content.ReadFromJsonAsync<CompleteToDoEndpoint.ToDoResponse>(JsonOptions);
        Assert.NotNull(completed);
        Assert.True(completed.Done);
        Assert.Null(completed.CompletionNotes);
    }

    [Fact]
    public async Task Put_PreservesTitleAndCategory()
    {
        var id = await SeedToDoAsync();
        var request = new CompleteToDoEndpoint.CompleteToDoRequest("Notes");

        var response = await _client.PutAsJsonAsync($"/api/todo/{id}/complete", request, JsonOptions);

        var completed = await response.Content.ReadFromJsonAsync<CompleteToDoEndpoint.ToDoResponse>(JsonOptions);
        Assert.NotNull(completed);
        Assert.Equal("Outstanding ToDo", completed.Title);
        Assert.Equal(_categoryId, completed.CategoryId);
    }

    [Fact]
    public async Task Put_WhenIdDoesNotExist_ReturnsProblemDetails()
    {
        var request = new CompleteToDoEndpoint.CompleteToDoRequest("Notes");

        var response = await _client.PutAsJsonAsync("/api/todo/999999/complete", request, JsonOptions);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}
