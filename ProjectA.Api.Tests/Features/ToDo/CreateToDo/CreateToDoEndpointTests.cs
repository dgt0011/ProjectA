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
    private const string TestCategory = "Create Test Category";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _client;
    private readonly IDbConnectionFactory _connectionFactory;

    public CreateToDoEndpointTests(ApiFactory factory)
    {
        _client = factory.CreateClient();
        _connectionFactory = factory.Services.GetRequiredService<IDbConnectionFactory>();
    }

    public async Task InitializeAsync() => await CleanUpAsync();

    public async Task DisposeAsync() => await CleanUpAsync();

    private async Task CleanUpAsync()
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        await connection.ExecuteAsync("DELETE FROM todos WHERE category = @Category;", new { Category = TestCategory });
    }

    [Fact]
    public async Task Post_WithValidRequest_CreatesToDoAndReturnsCreated()
    {
        var request = new CreateToDoEndpoint.CreateToDoRequest("New ToDo", TestCategory, "A description");

        var response = await _client.PostAsJsonAsync("/api/todo", request, JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var created = await response.Content.ReadFromJsonAsync<CreateToDoEndpoint.ToDoResponse>(JsonOptions);
        Assert.NotNull(created);
        Assert.True(created.Id > 0);
        Assert.Contains($"/api/todo/{created.Id}", response.Headers.Location!.ToString());
        Assert.Equal("New ToDo", created.Title);
        Assert.Equal(TestCategory, created.Category);
        Assert.Equal("A description", created.Description);
        Assert.False(created.Done);
        Assert.NotNull(created.DateCreated);
        Assert.Null(created.DateModified);
    }

    [Fact]
    public async Task Post_WithMissingTitle_ReturnsValidationProblem()
    {
        var request = new CreateToDoEndpoint.CreateToDoRequest("", TestCategory, null);

        var response = await _client.PostAsJsonAsync("/api/todo", request, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemResponse>(JsonOptions);
        Assert.NotNull(problem);
        Assert.True(problem.Errors.ContainsKey("Title"));
    }

    [Fact]
    public async Task Post_WithMissingCategory_ReturnsValidationProblem()
    {
        var request = new CreateToDoEndpoint.CreateToDoRequest("A title", " ", null);

        var response = await _client.PostAsJsonAsync("/api/todo", request, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemResponse>(JsonOptions);
        Assert.NotNull(problem);
        Assert.True(problem.Errors.ContainsKey("Category"));
    }

    private sealed record ValidationProblemResponse(
        string? Type,
        string? Title,
        int? Status,
        Dictionary<string, string[]> Errors);
}
