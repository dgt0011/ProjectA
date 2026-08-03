using Dapper.Contrib.Extensions;
using Microsoft.AspNetCore.Http.HttpResults;
using ProjectA.Api.Data;

namespace ProjectA.Api.Features.AttachmentTypes.GetAttachmentTypeIcon;

public static class GetAttachmentTypeIconEndpoint
{
    public static void MapGetAttachmentTypeIcon(this RouteGroupBuilder group)
    {
        group.MapGet("{id}/icon", Handle)
            .WithName("GetAttachmentTypeIcon")
            .WithSummary("Get an attachment type's icon")
            .WithDescription("Returns the raw icon image bytes for an attachment type, or 404 if it has none.");
    }

    private static async Task<Results<FileContentHttpResult, NotFound>> Handle(
        uint id,
        IDbConnectionFactory connectionFactory,
        CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
        var entity = await connection.GetAsync<AttachmentTypeDto>((long)id);

        if (entity?.icon is null || entity.icon.Length == 0)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.File(entity.icon, entity.icon_content_type ?? "application/octet-stream");
    }
}
