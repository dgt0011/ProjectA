using Dapper.Contrib.Extensions;
using Microsoft.AspNetCore.Http.HttpResults;
using Npgsql;
using ProjectA.Api.Data;

namespace ProjectA.Api.Features.AttachmentTypes.DeleteAttachmentType;

public static class DeleteAttachmentTypeEndpoint
{
    public static void MapDeleteAttachmentType(this RouteGroupBuilder group)
    {
        group.MapDelete("{id}", Handle)
            .WithName("DeleteAttachmentType")
            .WithSummary("Delete an attachment type")
            .WithDescription("Permanently removes an attachment type by Id.")
            .RequireAuthorization();
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> Handle(
        uint id,
        IDbConnectionFactory connectionFactory,
        CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);

        bool deleted;
        try
        {
            deleted = await connection.DeleteAsync(new AttachmentTypeDto { id = (long)id });
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.ForeignKeyViolation)
        {
            // attachments.attachment_type_id references attachment_types(id).
            return TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Attachment type is in use",
                detail: $"Attachment type {id} is still assigned to one or more attachments and cannot be deleted.",
                type: "https://tools.ietf.org/html/rfc7231#section-6.5.8");
        }

        if (!deleted)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Attachment type not found",
                detail: $"No attachment type exists with id {id}.",
                type: "https://tools.ietf.org/html/rfc7231#section-6.5.4");
        }

        return TypedResults.NoContent();
    }
}
