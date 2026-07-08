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
    private const string TestCategory = "Update Test Category";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _client;
    private readonly IDbConnectionFactory _connectionFactory;

    public UpdateToDoEndpointTests(ApiFactory factory)
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

    private async Task<long> SeedToDoAsync(bool actioned = false)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        return await connection.QuerySingleAsync<long>(
            "INSERT INTO todos (title, actioned, category, description, date_created) " +
            "VALUES ('Original Title', @Actioned, @Category, 'Original description', now()) " +
            "RETURNING id;",
            new { Actioned = actioned, Category = TestCategory });
    }

    [Fact]
    public async Task Put_WithValidRequest_UpdatesAndReturnsOk()
    {
        var id = await SeedToDoAsync();
        var request = new UpdateToDoEndpoint.UpdateToDoRequest("Updated Title", TestCategory, "Updated description", true);

        var response = await _client.PutAsJsonAsync($"/api/todo/{id}", request, JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await response.Content.ReadFromJsonAsync<UpdateToDoEndpoint.ToDoResponse>(JsonOptions);
        Assert.NotNull(updated);
        Assert.Equal(id, updated.Id);
        Assert.Equal("Updated Title", updated.Title);
        Assert.Equal("Updated description", updated.Description);
        Assert.True(updated.Done);
        Assert.NotNull(updated.DateModified);
    }

    [Fact]
    public async Task Put_WhenIdDoesNotExist_ReturnsProblemDetails()
    {
        var request = new UpdateToDoEndpoint.UpdateToDoRequest("Doesn't matter", TestCategory, null, false);

        var response = await _client.PutAsJsonAsync("/api/todo/999999", request, JsonOptions);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Put_WithMissingTitle_ReturnsValidationProblem()
    {
        var id = await SeedToDoAsync();
        var request = new UpdateToDoEndpoint.UpdateToDoRequest(" ", TestCategory, null, false);

        var response = await _client.PutAsJsonAsync($"/api/todo/{id}", request, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Put_CanMarkToDoAsDone()
    {
        var id = await SeedToDoAsync(actioned: false);
        var request = new UpdateToDoEndpoint.UpdateToDoRequest("Original Title", TestCategory, "Original description", true);

        var response = await _client.PutAsJsonAsync($"/api/todo/{id}", request, JsonOptions);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var getResponse = await _client.GetAsync($"/api/todo/{id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
    }
}
