using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ProjectA.Api.Data;
using ProjectA.Api.Features.Categories.CreateCategory;
using Xunit;

namespace ProjectA.Api.Tests.Features.Categories.CreateCategory;

[Collection(nameof(ApiCollection))]
public class CreateCategoryEndpointTests : IAsyncLifetime
{
    private const string TitlePrefix = "Create Test Category";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _client;
    private readonly IDbConnectionFactory _connectionFactory;

    public CreateCategoryEndpointTests(ApiFactory factory)
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
            "DELETE FROM categories WHERE title LIKE @Pattern;",
            new { Pattern = $"{TitlePrefix}%" });
    }

    [Fact]
    public async Task Post_WithValidRequest_CreatesCategoryAndReturnsCreated()
    {
        var request = new CreateCategoryEndpoint.CreateCategoryRequest($"{TitlePrefix} New", "A description");

        var response = await _client.PostAsJsonAsync("/api/categories", request, JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var created = await response.Content.ReadFromJsonAsync<CreateCategoryEndpoint.CategoryResponse>(JsonOptions);
        Assert.NotNull(created);
        Assert.True(created.Id > 0);
        Assert.Contains($"/api/categories/{created.Id}", response.Headers.Location!.ToString());
        Assert.Equal($"{TitlePrefix} New", created.Title);
        Assert.Equal("A description", created.Description);
    }

    [Fact]
    public async Task Post_WithMissingTitle_ReturnsValidationProblem()
    {
        var request = new CreateCategoryEndpoint.CreateCategoryRequest(" ", null);

        var response = await _client.PostAsJsonAsync("/api/categories", request, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemResponse>(JsonOptions);
        Assert.NotNull(problem);
        Assert.True(problem.Errors.ContainsKey("Title"));
    }

    private sealed record ValidationProblemResponse(
        string? Type,
        string? Title,
        int? Status,
        Dictionary<string, string[]> Errors);
}
