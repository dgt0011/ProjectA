using System.Net;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ProjectA.Api.Data;
using Xunit;

namespace ProjectA.Api.Tests.Features.Categories.DeleteCategory;

[Collection(nameof(ApiCollection))]
public class DeleteCategoryEndpointTests : IAsyncLifetime
{
    private const string TitlePrefix = "Delete Test Category";

    private readonly HttpClient _client;
    private readonly IDbConnectionFactory _connectionFactory;

    public DeleteCategoryEndpointTests(ApiFactory factory)
    {
        _client = factory.CreateAuthenticatedClient();
        _connectionFactory = factory.Services.GetRequiredService<IDbConnectionFactory>();
    }

    public async Task InitializeAsync() => await CleanUpAsync();

    public async Task DisposeAsync() => await CleanUpAsync();

    private async Task CleanUpAsync()
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();

        // Delete any blocking todos first - otherwise the FK constraint that this cleanup
        // exists to work around would stop the category rows themselves being removed.
        await connection.ExecuteAsync(
            "DELETE FROM todos WHERE category_id IN (SELECT id FROM categories WHERE title LIKE @Pattern);",
            new { Pattern = $"{TitlePrefix}%" });
        await connection.ExecuteAsync(
            "DELETE FROM categories WHERE title LIKE @Pattern;",
            new { Pattern = $"{TitlePrefix}%" });
    }

    private async Task<long> SeedCategoryAsync(string suffix)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        return await connection.QuerySingleAsync<long>(
            "INSERT INTO categories (title) VALUES (@Title) RETURNING id;",
            new { Title = $"{TitlePrefix} {suffix}" });
    }

    [Fact]
    public async Task Delete_WhenExists_RemovesCategoryAndReturnsNoContent()
    {
        var id = await SeedCategoryAsync("ToDelete");

        var response = await _client.DeleteAsync($"/api/categories/{id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var followUp = await _client.GetAsync($"/api/categories/{id}");
        Assert.Equal(HttpStatusCode.NotFound, followUp.StatusCode);
    }

    [Fact]
    public async Task Delete_WhenMissing_ReturnsProblemDetails()
    {
        var response = await _client.DeleteAsync("/api/categories/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Delete_WhenReferencedByToDo_ReturnsConflict()
    {
        var id = await SeedCategoryAsync("InUse");

        using (var connection = await _connectionFactory.CreateConnectionAsync())
        {
            await connection.ExecuteAsync(
                "INSERT INTO todos (title, actioned, category, category_id) " +
                "VALUES ('Blocking ToDo', false, 'Some Category', @CategoryId);",
                new { CategoryId = id });
        }

        var response = await _client.DeleteAsync($"/api/categories/{id}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}
