using Dapper.Contrib.Extensions;
using Microsoft.AspNetCore.Http.HttpResults;
using Npgsql;
using ProjectA.Api.Data;

namespace ProjectA.Api.Features.Attachments.DeleteAttachment;

public static class DeleteAttachmentEndpoint
{
    public static void MapDeleteAttachment(this RouteGroupBuilder group)
    {
        group.MapDelete("{id}", Handle)
            .WithName("DeleteAttachment")
            .WithSummary("Delete an attachment")
            .WithDescription("Permanently removes an attachment by Id.")
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
            deleted = await connection.DeleteAsync(new AttachmentDto { id = (long)id });
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.ForeignKeyViolation)
        {
            // note_attachments and project_attachments reference attachments(id).
            return TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Attachment is in use",
                detail: $"Attachment {id} is still referenced by other records and cannot be deleted.",
                type: "https://tools.ietf.org/html/rfc7231#section-6.5.8");
        }

        if (!deleted)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Attachment not found",
                detail: $"No attachment exists with id {id}.",
                type: "https://tools.ietf.org/html/rfc7231#section-6.5.4");
        }

        return TypedResults.NoContent();
    }
}
