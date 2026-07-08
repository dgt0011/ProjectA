using Dapper.Contrib.Extensions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using ProjectA.Api.Data;
using ProjectA.Api.Features.ToDo;

namespace ProjectA.Api.Features.ToDo.GetToDoList;

public static class GetToDoListEndpoint
{
    public static void MapGetToDoList(this RouteGroupBuilder group)
    {
        group.MapGet("", Handle)
            .WithName("GetToDoList")
            .WithSummary("List ToDo items")
            .WithDescription(
                "Returns outstanding ToDo items. Pass includeDone=true to include completed items.");
    }

    private static async Task<Ok<List<ToDoListItemResponse>>> Handle(
        IDbConnectionFactory connectionFactory,
        [FromQuery] bool includeDone = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);

            // Dapper.Contrib's GetAllAsync has no filtering support, so the includeDone
            // filter is applied in memory. Fine at this table's size; if `todos` grows large
            // enough for this to matter, that's a sign this slice should go back to a
            // hand-written filtered query.
            var entities = await connection.GetAllAsync<ToDoEntity>();

            var items = entities
                .Where(entity => includeDone || !entity.actioned)
                .Select(entity => new ToDoListItemResponse(
                    entity.id,
                    entity.category,
                    entity.title,
                    entity.description,
                    entity.date_created,
                    entity.date_modified,
                    entity.actioned))
                .ToList();

            return TypedResults.Ok(items);
        }
        catch (Exception)
        {
            // TODO: Some logging is necessary
            return TypedResults.Ok(new List<ToDoListItemResponse>());
        }
    }

    // Shape returned to callers of this endpoint - owned by this slice, not shared.
    public sealed record ToDoListItemResponse(
        long Id,
        string Category,
        string Title,
        string? Description,
        DateTime? DateCreated,
        DateTime? DateModified,
        bool Done);
}
