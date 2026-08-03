using Dapper.Contrib.Extensions;
using Microsoft.AspNetCore.Http.HttpResults;
using ProjectA.Api.Data;

namespace ProjectA.Api.Features.Attachments.GetAttachmentById;

public static class GetAttachmentByIdEndpoint
{
    public static void MapGetAttachmentById(this RouteGroupBuilder group)
    {
        group.MapGet("{id}", Handle)
            .WithName("GetAttachmentById")
            .WithSummary("Get an attachment by Id")
            .WithDescription("Returns a single attachment by Id.");
    }

    private static async Task<Results<Ok<AttachmentResponse>, ProblemHttpResult>> Handle(
        uint id,
        IDbConnectionFactory connectionFactory,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
            var entity = await connection.GetAsync<AttachmentDto>((long)id);

            if (entity is not null)
            {
                var response = new AttachmentResponse(
                    entity.id,
                    entity.title,
                    entity.description,
                    entity.file_path,
                    entity.attachment_type_id,
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
            title: "Attachment not found",
            detail: $"No attachment exists with id {id}.",
            type: "https://tools.ietf.org/html/rfc7231#section-6.5.4");
    }

    // Shape returned to callers of this endpoint - owned by this slice, not shared.
    public sealed record AttachmentResponse(
        long Id,
        string? Title,
        string? Description,
        string FilePath,
        long? AttachmentTypeId,
        DateTime DateCreated,
        DateTime? DateModified);
}
