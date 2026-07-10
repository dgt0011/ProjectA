using Dapper.Contrib.Extensions;
using Microsoft.AspNetCore.Http.HttpResults;
using ProjectA.Api.Data;

namespace ProjectA.Api.Features.Attachments.GetAttachmentList;

public static class GetAttachmentListEndpoint
{
    public static void MapGetAttachmentList(this RouteGroupBuilder group)
    {
        group.MapGet("", Handle)
            .WithName("GetAttachmentList")
            .WithSummary("List attachments")
            .WithDescription("Returns all attachments.");
    }

    private static async Task<Ok<List<AttachmentListItemResponse>>> Handle(
        IDbConnectionFactory connectionFactory,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
            var entities = await connection.GetAllAsync<AttachmentDto>();

            var items = entities
                .Select(entity => new AttachmentListItemResponse(
                    entity.id,
                    entity.title,
                    entity.description,
                    entity.s3_arn,
                    entity.date_created,
                    entity.date_modified))
                .ToList();

            return TypedResults.Ok(items);
        }
        catch (Exception)
        {
            // TODO: Some logging is necessary
            return TypedResults.Ok(new List<AttachmentListItemResponse>());
        }
    }

    // Shape returned to callers of this endpoint - owned by this slice, not shared.
    public sealed record AttachmentListItemResponse(
        long Id,
        string? Title,
        string? Description,
        string S3Arn,
        DateTime DateCreated,
        DateTime? DateModified);
}
