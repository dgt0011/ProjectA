using Dapper.Contrib.Extensions;
using Microsoft.AspNetCore.Http.HttpResults;
using ProjectA.Api.Data;

namespace ProjectA.Api.Features.AttachmentTypes.GetAttachmentTypeList;

public static class GetAttachmentTypeListEndpoint
{
    public static void MapGetAttachmentTypeList(this RouteGroupBuilder group)
    {
        group.MapGet("", Handle)
            .WithName("GetAttachmentTypeList")
            .WithSummary("List attachment types")
            .WithDescription("Returns all attachment types.");
    }

    private static async Task<Ok<List<AttachmentTypeListItemResponse>>> Handle(
        IDbConnectionFactory connectionFactory,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
            var entities = await connection.GetAllAsync<AttachmentTypeDto>();

            var items = entities
                .Select(entity => new AttachmentTypeListItemResponse(entity.id, entity.title, entity.color, entity.icon is not null))
                .ToList();

            return TypedResults.Ok(items);
        }
        catch (Exception)
        {
            // TODO: Some logging is necessary
            return TypedResults.Ok(new List<AttachmentTypeListItemResponse>());
        }
    }

    // Shape returned to callers of this endpoint - owned by this slice, not shared.
    public sealed record AttachmentTypeListItemResponse(long Id, string Title, string? Color, bool HasIcon);
}
