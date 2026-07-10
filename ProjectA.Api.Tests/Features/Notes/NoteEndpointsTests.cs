using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ProjectA.Api.Data;
using ProjectA.Api.Features.Notes.GetNoteById;
using ProjectA.Api.Features.Notes.GetNoteList;
using Xunit;

namespace ProjectA.Api.Tests.Features.Notes;

[Collection(nameof(ApiCollection))]
public class NoteEndpointsTests : IAsyncLifetime
{
    private const string TitlePrefix = "List Test Note";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _client;
    private readonly IDbConnectionFactory _connectionFactory;

    public NoteEndpointsTests(ApiFactory factory)
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

    [Fact]
    public async Task GetList_ReturnsAllNotes()
    {
        using (var connection = await _connectionFactory.CreateConnectionAsync())
        {
            await connection.ExecuteAsync(
                "INSERT INTO notes (title, body) VALUES (@TitleA, 'Body A'), (@TitleB, NULL);",
                new { TitleA = $"{TitlePrefix} A", TitleB = $"{TitlePrefix} B" });
        }

        var response = await _client.GetAsync("/api/notes");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var notes = await response.Content.ReadFromJsonAsync<List<GetNoteListEndpoint.NoteListItemResponse>>(JsonOptions);
        Assert.NotNull(notes);

        var seeded = notes.Where(n => n.Title != null && n.Title.StartsWith(TitlePrefix)).ToList();
        Assert.Equal(2, seeded.Count);
        Assert.Contains(seeded, n => n.Title == $"{TitlePrefix} A" && n.Body == "Body A");
        Assert.Contains(seeded, n => n.Title == $"{TitlePrefix} B" && n.Body == null);
    }

    [Fact]
    public async Task GetById_WhenExists_ReturnsNote()
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        var id = await connection.QuerySingleAsync<long>(
            "INSERT INTO notes (title, body) VALUES (@Title, 'Some body') RETURNING id;",
            new { Title = $"{TitlePrefix} ById" });

        var response = await _client.GetAsync($"/api/notes/{id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var note = await response.Content.ReadFromJsonAsync<GetNoteByIdEndpoint.NoteResponse>(JsonOptions);
        Assert.NotNull(note);
        Assert.Equal(id, note.Id);
        Assert.Equal($"{TitlePrefix} ById", note.Title);
        Assert.Equal("Some body", note.Body);
    }

    [Fact]
    public async Task GetById_WhenMissing_ReturnsProblemDetails()
    {
        var response = await _client.GetAsync("/api/notes/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}
