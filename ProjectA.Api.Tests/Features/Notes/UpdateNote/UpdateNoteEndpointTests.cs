using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ProjectA.Api.Data;
using ProjectA.Api.Features.Notes.UpdateNote;
using Xunit;

namespace ProjectA.Api.Tests.Features.Notes.UpdateNote;

[Collection(nameof(ApiCollection))]
public class UpdateNoteEndpointTests : IAsyncLifetime
{
    private const string TitlePrefix = "Update Test Note";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _client;
    private readonly IDbConnectionFactory _connectionFactory;

    public UpdateNoteEndpointTests(ApiFactory factory)
    {
        _client = factory.CreateClient();
        _connectionFactory = factory.Services.GetRequiredService<IDbConnectionFactory>();
    }

    public async Task InitializeAsync() => await CleanUpAsync();

    public async Task DisposeAsync() => await CleanUpAsync();

    private async Task CleanUpAsync()
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        await connection.ExecuteAsync("DELETE FROM notes WHERE title LIKE @Pattern;", new { Pattern = $"{TitlePrefix}%" });
    }

    private async Task<long> SeedNoteAsync()
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        return await connection.QuerySingleAsync<long>(
            "INSERT INTO notes (title, body) VALUES (@Title, 'Original body') RETURNING id;",
            new { Title = $"{TitlePrefix} Original" });
    }

    [Fact]
    public async Task Put_WithValidRequest_UpdatesAndReturnsOk()
    {
        var id = await SeedNoteAsync();
        var request = new UpdateNoteEndpoint.UpdateNoteRequest($"{TitlePrefix} Updated", "Updated body");

        var response = await _client.PutAsJsonAsync($"/api/notes/{id}", request, JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await response.Content.ReadFromJsonAsync<UpdateNoteEndpoint.NoteResponse>(JsonOptions);
        Assert.NotNull(updated);
        Assert.Equal(id, updated.Id);
        Assert.Equal($"{TitlePrefix} Updated", updated.Title);
        Assert.Equal("Updated body", updated.Body);
        Assert.NotNull(updated.DateModified);
    }

    [Fact]
    public async Task Put_WhenIdDoesNotExist_ReturnsProblemDetails()
    {
        var request = new UpdateNoteEndpoint.UpdateNoteRequest($"{TitlePrefix} Missing", null);

        var response = await _client.PutAsJsonAsync("/api/notes/999999", request, JsonOptions);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Put_WithNeitherTitleNorBody_ReturnsValidationProblem()
    {
        var id = await SeedNoteAsync();
        var request = new UpdateNoteEndpoint.UpdateNoteRequest(null, null);

        var response = await _client.PutAsJsonAsync($"/api/notes/{id}", request, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
