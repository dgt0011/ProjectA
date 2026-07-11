using Dapper.Contrib.Extensions;
using Microsoft.AspNetCore.Http.HttpResults;
using ProjectA.Api.Data;

namespace ProjectA.Api.Features.Notes.CreateNote;

public static class CreateNoteEndpoint
{
    public static void MapCreateNote(this RouteGroupBuilder group)
    {
        group.MapPost("", Handle)
            .WithName("CreateNote")
            .WithSummary("Create a note")
            .WithDescription("Creates a new note.")
            .RequireAuthorization();
    }

    private static async Task<Results<CreatedAtRoute<NoteResponse>, ValidationProblem>> Handle(
        CreateNoteRequest request,
        IDbConnectionFactory connectionFactory,
        CancellationToken cancellationToken = default)
    {
        var errors = Validate(request);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var entity = new NoteDto
        {
            title = request.Title,
            body = request.Body,
            date_created = DateTime.UtcNow
        };

        using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
        await connection.InsertAsync(entity);

        var response = new NoteResponse(entity.id, entity.title, entity.body, entity.date_created, entity.date_modified);

        return TypedResults.CreatedAtRoute(response, "GetNoteById", new { id = response.Id });
    }

    private static Dictionary<string, string[]> Validate(CreateNoteRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        // Neither column is NOT NULL in the schema, but a note with no title and no body
        // isn't a usable record, so this endpoint requires at least one of them.
        if (string.IsNullOrWhiteSpace(request.Title) && string.IsNullOrWhiteSpace(request.Body))
        {
            const string message = "Either Title or Body is required.";
            errors[nameof(request.Title)] = [message];
            errors[nameof(request.Body)] = [message];
        }

        return errors;
    }

    // Request body accepted by this endpoint - owned by this slice, not shared.
    public sealed record CreateNoteRequest(string? Title, string? Body);

    // Shape returned to callers of this endpoint - owned by this slice, not shared.
    public sealed record NoteResponse(
        long Id,
        string? Title,
        string? Body,
        DateTime DateCreated,
        DateTime? DateModified);
}
