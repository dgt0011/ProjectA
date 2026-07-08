using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ProjectA.Api.Data;
using ProjectA.Api.Features.Categories.GetCategoryById;
using ProjectA.Api.Features.Categories.GetCategoryList;
using Xunit;

namespace ProjectA.Api.Tests.Features.Categories;

[Collection(nameof(ApiCollection))]
public class CategoryEndpointsTests : IAsyncLifetime
{
    private const string TitlePrefix = "List Test Category";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _client;
    private readonly IDbConnectionFactory _connectionFactory;

    public CategoryEndpointsTests(ApiFactory factory)
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
            "DELETE FROM categories WHERE title LIKE @Pattern;",
            new { Pattern = $"{TitlePrefix}%" });
    }

    private async Task SeedAsync(params (string Title, string? Description)[] categories)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        foreach (var (title, description) in categories)
        {
            await connection.ExecuteAsync(
                "INSERT INTO categories (title, description) VALUES (@Title, @Description);",
                new { Title = title, Description = description });
        }
    }

    [Fact]
    public async Task GetList_ReturnsAllCategories()
    {
        await SeedAsync(
            ($"{TitlePrefix} A", "Description A"),
            ($"{TitlePrefix} B", null));

        var response = await _client.GetAsync("/api/categories");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var categories = await response.Content
            .ReadFromJsonAsync<List<GetCategoryListEndpoint.CategoryListItemResponse>>(JsonOptions);
        Assert.NotNull(categories);

        // Filter down to this test's own rows - `categories` is shared, unfiltered state,
        // so asserting on the raw total would be fragile against other tests' data.
        var seeded = categories.Where(c => c.Title.StartsWith(TitlePrefix)).ToList();
        Assert.Equal(2, seeded.Count);
        Assert.Contains(seeded, c => c.Title == $"{TitlePrefix} A" && c.Description == "Description A");
        Assert.Contains(seeded, c => c.Title == $"{TitlePrefix} B" && c.Description == null);
    }

    [Fact]
    public async Task GetById_WhenExists_ReturnsCategory()
    {
        await SeedAsync(($"{TitlePrefix} ById", "Some description"));

        using var connection = await _connectionFactory.CreateConnectionAsync();
        var id = await connection.QuerySingleAsync<long>(
            "SELECT id FROM categories WHERE title = @Title;",
            new { Title = $"{TitlePrefix} ById" });

        var response = await _client.GetAsync($"/api/categories/{id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var category = await response.Content
            .ReadFromJsonAsync<GetCategoryByIdEndpoint.CategoryResponse>(JsonOptions);
        Assert.NotNull(category);
        Assert.Equal(id, category.Id);
        Assert.Equal($"{TitlePrefix} ById", category.Title);
        Assert.Equal("Some description", category.Description);
    }

    [Fact]
    public async Task GetById_WhenMissing_ReturnsProblemDetails()
    {
        var response = await _client.GetAsync("/api/categories/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsResponse>(JsonOptions);
        Assert.NotNull(problem);
        Assert.Equal(404, problem.Status);
        Assert.Equal("Category not found", problem.Title);
    }

    private sealed record ProblemDetailsResponse(
        string? Type,
        string? Title,
        int? Status,
        string? Detail,
        string? Instance);
}
