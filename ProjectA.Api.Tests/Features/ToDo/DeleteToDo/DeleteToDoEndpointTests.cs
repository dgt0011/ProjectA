using System.Net;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ProjectA.Api.Data;
using Xunit;

namespace ProjectA.Api.Tests.Features.ToDo.DeleteToDo;

[Collection(nameof(ApiCollection))]
public class DeleteToDoEndpointTests : IAsyncLifetime
{
    private const string TestCategory = "Delete Test Category";

    private readonly HttpClient _client;
    private readonly IDbConnectionFactory _connectionFactory;

    public DeleteToDoEndpointTests(ApiFactory factory)
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

    private async Task<long> SeedToDoAsync()
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        return await connection.QuerySingleAsync<long>(
            "INSERT INTO todos (title, actioned, category, description, date_created) " +
            "VALUES ('To be deleted', false, @Category, null, now()) RETURNING id;",
            new { Category = TestCategory });
    }

    [Fact]
    public async Task Delete_WhenExists_RemovesToDoAndReturnsNoContent()
    {
        var id = await SeedToDoAsync();

        var response = await _client.DeleteAsync($"/api/todo/{id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var followUp = await _client.GetAsync($"/api/todo/{id}");
        Assert.Equal(HttpStatusCode.NotFound, followUp.StatusCode);
    }

    [Fact]
    public async Task Delete_WhenMissing_ReturnsProblemDetails()
    {
        var response = await _client.DeleteAsync("/api/todo/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}
