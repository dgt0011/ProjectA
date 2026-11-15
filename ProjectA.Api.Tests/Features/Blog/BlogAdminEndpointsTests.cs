using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ProjectA.Api.Data;
using ProjectA.Api.Features.Blog.Categories;
using ProjectA.Api.Features.Blog.Public.GetBlogSite;
using ProjectA.Api.Features.Blog.Settings;
using Xunit;

namespace ProjectA.Api.Tests.Features.Blog;

[Collection(nameof(ApiCollection))]
public class BlogAdminEndpointsTests : IAsyncLifetime
{
    private const string TitlePrefix = "Blogadmintest";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _client;
    private readonly HttpClient _anonymousClient;
    private readonly IDbConnectionFactory _connectionFactory;

    public BlogAdminEndpointsTests(ApiFactory factory)
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
        await connection.ExecuteAsync("DELETE FROM blog_categories WHERE title ILIKE @Pattern;", new { Pattern = $"{TitlePrefix}%" });
    }

    [Fact]
    public async Task Category_DuplicateTitleDifferingOnlyByCase_IsRejected()
    {
        var created = await _client.PostAsJsonAsync(
            "/api/blog/categories", new BlogCategoryEndpoints.BlogCategoryRequest($"{TitlePrefix} AWS", null), JsonOptions);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var duplicate = await _client.PostAsJsonAsync(
            "/api/blog/categories", new BlogCategoryEndpoints.BlogCategoryRequest($"{TitlePrefix} aws", null), JsonOptions);
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
    }

    [Fact]
    public async Task Settings_Update_IsReflectedOnPublicSiteEndpoint()
    {
        var original = await _client.GetFromJsonAsync<BlogSettingsEndpoints.BlogSettingsResponse>("/api/blog/settings", JsonOptions);
        try
        {
            var request = new BlogSettingsEndpoints.UpdateBlogSettingsRequest(
                $"{TitlePrefix} Bits", "Desc", 5, false, null, null, "<meta name=\"x\">", null);
            var response = await _client.PutAsJsonAsync("/api/blog/settings", request, JsonOptions);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var site = await _anonymousClient.GetFromJsonAsync<GetBlogSiteEndpoint.BlogSiteResponse>("/api/blog/site", JsonOptions);
            Assert.Equal($"{TitlePrefix} Bits", site!.Title);
            Assert.Equal(5, site.ItemsPerPage);
            Assert.Equal("<meta name=\"x\">", site.HeaderScript);
        }
        finally
        {
            await _client.PutAsJsonAsync("/api/blog/settings", new BlogSettingsEndpoints.UpdateBlogSettingsRequest(
                original!.Title, original.Description, original.ItemsPerPage, original.IncludeFeatured,
                original.Cover, original.Logo, original.HeaderScript, original.FooterScript), JsonOptions);
        }
    }

    [Fact]
    public async Task Settings_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _anonymousClient.GetAsync("/api/blog/settings");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
