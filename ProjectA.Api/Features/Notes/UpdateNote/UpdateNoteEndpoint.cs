using Dapper.Contrib.Extensions;
using Microsoft.AspNetCore.Http.HttpResults;
using ProjectA.Api.Data;

namespace ProjectA.Api.Features.Notes.UpdateNote;

public static class UpdateNoteEndpoint
{
    public static void MapUpdateNote(this RouteGroupBuilder group)
    {
        group.MapPut("{id}", Handle)
            .WithName("UpdateNote")
            .WithSummary("Update a note")
            .WithDescription("Replaces an existing note's title and body.")
            .RequireAuthorization();
    }

    private static async Task<Results<Ok<NoteResponse>, ValidationProblem, ProblemHttpResult>> Handle(
        uint id,
        UpdateNoteRequest request,
        IDbConnectionFactory connectionFactory,
        CancellationToken cancellationToken = default)
    {
        var errors = Validate(request);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var notFound = TypedResults.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Note not found",
            detail: $"No note exists with id {id}.",
            type: "https://tools.ietf.org/html/rfc7231#section-6.5.4");

        using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);

        var entity = await connection.GetAsync<NoteDto>((long)id);
        if (entity is null)
        {
            return notFound;
        }

        entity.title = request.Title;
        entity.body = request.Body;
        entity.date_modified = DateTime.UtcNow;

        var updated = await connection.UpdateAsync(entity);
        if (!updated)
        {
            return notFound;
        }

        var response = new NoteResponse(entity.id, entity.title, entity.body, entity.date_created, entity.date_modified);

        return TypedResults.Ok(response);
    }

    private static Dictionary<string, string[]> Validate(UpdateNoteRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Title) && string.IsNullOrWhiteSpace(request.Body))
        {
            const string message = "Either Title or Body is required.";
            errors[nameof(request.Title)] = [message];
            errors[nameof(request.Body)] = [message];
        }

        return errors;
    }

    // Request body accepted by this endpoint - owned by this slice, not shared.
    public sealed record UpdateNoteRequest(string? Title, string? Body);

    // Shape returned to callers of this endpoint - owned by this slice, not shared.
    public sealed record NoteResponse(
        long Id,
        string? Title,
        string? Body,
        DateTime DateCreated,
        DateTime? DateModified);
}
