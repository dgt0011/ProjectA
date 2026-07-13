using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ProjectA.Api.Data;
using ProjectA.Api.Features.Notes.CreateNote;
using Xunit;

namespace ProjectA.Api.Tests.Features.Notes.CreateNote;

[Collection(nameof(ApiCollection))]
public class CreateNoteEndpointTests : IAsyncLifetime
{
    private const string TitlePrefix = "Create Test Note";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _client;
    private readonly IDbConnectionFactory _connectionFactory;

    public CreateNoteEndpointTests(ApiFactory factory)
    {
        _client = factory.CreateAuthenticatedClient();
        _connectionFactory = factory.Services.GetRequiredService<IDbConnectionFactory>();
    }

    public async Task InitializeAsync() => await CleanUpAsync();

    public async Task DisposeAsync() => await CleanUpAsync();

    private async Task CleanUpAsync()
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        await connection.ExecuteAsync("DELETE FROM notes WHERE title LIKE @Pattern;", new { Pattern = $"{TitlePrefix}%" });
    }

    [Fact]
    public async Task Post_WithValidRequest_CreatesNoteAndReturnsCreated()
    {
        var request = new CreateNoteEndpoint.CreateNoteRequest($"{TitlePrefix} New", "A short summary", "Some body text");

        var response = await _client.PostAsJsonAsync("/api/notes", request, JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var created = await response.Content.ReadFromJsonAsync<CreateNoteEndpoint.NoteResponse>(JsonOptions);
        Assert.NotNull(created);
        Assert.True(created.Id > 0);
        Assert.Contains($"/api/notes/{created.Id}", response.Headers.Location!.ToString());
        Assert.Equal($"{TitlePrefix} New", created.Title);
        Assert.Equal("A short summary", created.Description);
        Assert.Equal("Some body text", created.Body);
    }

    [Fact]
    public async Task Post_WithOnlyBody_Succeeds()
    {
        // Title is nullable in the schema; a body-only note should still be creatable.
        var request = new CreateNoteEndpoint.CreateNoteRequest(null, null, $"{TitlePrefix} body-only content");

        var response = await _client.PostAsJsonAsync("/api/notes", request, JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Post_WithNeitherTitleNorBody_ReturnsValidationProblem()
    {
        var request = new CreateNoteEndpoint.CreateNoteRequest(null, null, null);

        var response = await _client.PostAsJsonAsync("/api/notes", request, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemResponse>(JsonOptions);
        Assert.NotNull(problem);
        Assert.True(problem.Errors.ContainsKey("Title"));
        Assert.True(problem.Errors.ContainsKey("Body"));
    }

    private sealed record ValidationProblemResponse(
        string? Type,
        string? Title,
        int? Status,
        Dictionary<string, string[]> Errors);
}
