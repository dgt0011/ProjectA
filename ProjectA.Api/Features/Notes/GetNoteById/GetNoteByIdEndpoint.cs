using Dapper.Contrib.Extensions;
using Microsoft.AspNetCore.Http.HttpResults;
using ProjectA.Api.Data;

namespace ProjectA.Api.Features.Notes.GetNoteById;

public static class GetNoteByIdEndpoint
{
    public static void MapGetNoteById(this RouteGroupBuilder group)
    {
        group.MapGet("{id}", Handle)
            .WithName("GetNoteById")
            .WithSummary("Get a note by Id")
            .WithDescription("Returns a single note by Id.");
    }

    private static async Task<Results<Ok<NoteResponse>, ProblemHttpResult>> Handle(
        uint id,
        IDbConnectionFactory connectionFactory,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
            var entity = await connection.GetAsync<NoteDto>((long)id);

            if (entity is not null)
            {
                var response = new NoteResponse(
                    entity.id,
                    entity.title,
                    entity.body,
                    entity.date_created,
                    entity.date_modified);

                return TypedResults.Ok(response);
            }
        }
        catch (Exception)
        {
            // TODO: Some logging is necessary
        }

        return TypedResults.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Note not found",
            detail: $"No note exists with id {id}.",
            type: "https://tools.ietf.org/html/rfc7231#section-6.5.4");
    }

    // Shape returned to callers of this endpoint - owned by this slice, not shared.
    public sealed record NoteResponse(
        long Id,
        string? Title,
        string? Body,
        DateTime DateCreated,
        DateTime? DateModified);
}
