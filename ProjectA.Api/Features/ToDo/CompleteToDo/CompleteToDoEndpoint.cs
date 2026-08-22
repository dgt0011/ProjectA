using Dapper.Contrib.Extensions;
using Microsoft.AspNetCore.Http.HttpResults;
using ProjectA.Api.Data;

namespace ProjectA.Api.Features.ToDo.CompleteToDo;

public static class CompleteToDoEndpoint
{
    public static void MapCompleteToDo(this RouteGroupBuilder group)
    {
        group.MapPut("{id}/complete", Handle)
            .WithName("CompleteToDo")
            .WithSummary("Complete a ToDo")
            .WithDescription(
                "Marks a ToDo as done, optionally recording free-text (Markdown) completion notes. " +
                "A dedicated action rather than a full UpdateToDo call, since completing a ToDo - " +
                "e.g. from the 'Complete' modal on a Project's Details page - only ever needs to " +
                "supply notes, not resend the ToDo's Title/CategoryId/Description/ProjectId.")
            .RequireAuthorization();
    }

    private static async Task<Results<Ok<ToDoResponse>, ProblemHttpResult>> Handle(
        uint id,
        CompleteToDoRequest request,
        IDbConnectionFactory connectionFactory,
        CancellationToken cancellationToken = default)
    {
        var notFound = TypedResults.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "ToDo not found",
            detail: $"No ToDo exists with id {id}.",
            type: "https://tools.ietf.org/html/rfc7231#section-6.5.4");

        using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);

        // Same fetch-then-write pattern as UpdateToDoEndpoint - Dapper.Contrib's UpdateAsync
        // writes every non-key property back, so fields this request doesn't carry (Title,
        // CategoryId, ProjectId, Description) have to come from the existing row, not defaults.
        var entity = await connection.GetAsync<ToDoDto>((long)id);
        if (entity is null)
        {
            return notFound;
        }

        entity.actioned = true;
        entity.completion_notes = request.Notes;
        entity.date_modified = DateTime.UtcNow;

        var updated = await connection.UpdateAsync(entity);
        if (!updated)
        {
            // Row was deleted between the read above and this write.
            return notFound;
        }

        var response = new ToDoResponse(
            entity.id,
            entity.category_id,
            entity.project_id,
            entity.title,
            entity.description,
            entity.completion_notes,
            entity.date_created,
            entity.date_modified,
            entity.actioned);

        return TypedResults.Ok(response);
    }

    // Request body accepted by this endpoint - owned by this slice, not shared.
    public sealed record CompleteToDoRequest(string? Notes);

    // Shape returned to callers of this endpoint - owned by this slice, not shared.
    public sealed record ToDoResponse(
        long Id,
        long? CategoryId,
        long? ProjectId,
        string Title,
        string? Description,
        string? CompletionNotes,
        DateTime? DateCreated,
        DateTime? DateModified,
        bool Done);
}
