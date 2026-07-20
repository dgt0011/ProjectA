using Dapper.Contrib.Extensions;
using Microsoft.AspNetCore.Http.HttpResults;
using Npgsql;
using ProjectA.Api.Data;

namespace ProjectA.Api.Features.Attachments.CreateAttachment;

public static class CreateAttachmentEndpoint
{
    public static void MapCreateAttachment(this RouteGroupBuilder group)
    {
        group.MapPost("", Handle)
            .WithName("CreateAttachment")
            .WithSummary("Create an attachment")
            .WithDescription(
                "Creates a new attachment record pointing at a locally-stored file, optionally " +
                "categorized with an attachment type.")
            .RequireAuthorization();
    }

    private static async Task<Results<CreatedAtRoute<AttachmentResponse>, ValidationProblem>> Handle(
        CreateAttachmentRequest request,
        IDbConnectionFactory connectionFactory,
        CancellationToken cancellationToken = default)
    {
        var errors = Validate(request);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var entity = new AttachmentDto
        {
            title = request.Title,
            description = request.Description,
            file_path = request.FilePath,
            attachment_type_id = request.AttachmentTypeId,
            date_created = DateTime.UtcNow
        };

        using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);

        try
        {
            await connection.InsertAsync(entity);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.ForeignKeyViolation)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.AttachmentTypeId)] = ["AttachmentTypeId does not refer to an existing attachment type."]
            });
        }

        var response = new AttachmentResponse(
            entity.id,
            entity.title,
            entity.description,
            entity.file_path,
            entity.attachment_type_id,
            entity.date_created,
            entity.date_modified);

        return TypedResults.CreatedAtRoute(response, "GetAttachmentById", new { id = response.Id });
    }

    private static Dictionary<string, string[]> Validate(CreateAttachmentRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.FilePath))
        {
            errors[nameof(request.FilePath)] = ["FilePath is required."];
        }

        return errors;
    }

    // Request body accepted by this endpoint - owned by this slice, not shared.
    public sealed record CreateAttachmentRequest(string? Title, string? Description, string FilePath, long? AttachmentTypeId);

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
