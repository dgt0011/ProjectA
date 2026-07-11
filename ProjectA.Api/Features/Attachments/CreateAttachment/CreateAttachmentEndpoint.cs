using Dapper.Contrib.Extensions;
using Microsoft.AspNetCore.Http.HttpResults;
using ProjectA.Api.Data;

namespace ProjectA.Api.Features.Attachments.CreateAttachment;

public static class CreateAttachmentEndpoint
{
    public static void MapCreateAttachment(this RouteGroupBuilder group)
    {
        group.MapPost("", Handle)
            .WithName("CreateAttachment")
            .WithSummary("Create an attachment")
            .WithDescription("Creates a new attachment record pointing at an S3 object.")
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
            s3_arn = request.S3Arn,
            date_created = DateTime.UtcNow
        };

        using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
        await connection.InsertAsync(entity);

        var response = new AttachmentResponse(
            entity.id,
            entity.title,
            entity.description,
            entity.s3_arn,
            entity.date_created,
            entity.date_modified);

        return TypedResults.CreatedAtRoute(response, "GetAttachmentById", new { id = response.Id });
    }

    private static Dictionary<string, string[]> Validate(CreateAttachmentRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.S3Arn))
        {
            errors[nameof(request.S3Arn)] = ["S3Arn is required."];
        }

        return errors;
    }

    // Request body accepted by this endpoint - owned by this slice, not shared.
    public sealed record CreateAttachmentRequest(string? Title, string? Description, string S3Arn);

    // Shape returned to callers of this endpoint - owned by this slice, not shared.
    public sealed record AttachmentResponse(
        long Id,
        string? Title,
        string? Description,
        string S3Arn,
        DateTime DateCreated,
        DateTime? DateModified);
}
