using Dapper.Contrib.Extensions;
using Microsoft.AspNetCore.Http.HttpResults;
using ProjectA.Api.Data;

namespace ProjectA.Api.Features.Attachments.UpdateAttachment;

public static class UpdateAttachmentEndpoint
{
    public static void MapUpdateAttachment(this RouteGroupBuilder group)
    {
        group.MapPut("{id}", Handle)
            .WithName("UpdateAttachment")
            .WithSummary("Update an attachment")
            .WithDescription("Replaces an existing attachment's title, description and S3 reference.")
            .RequireAuthorization();
    }

    private static async Task<Results<Ok<AttachmentResponse>, ValidationProblem, ProblemHttpResult>> Handle(
        uint id,
        UpdateAttachmentRequest request,
        IDbConnectionFactory connectionFactory,
        CancellationToken cancellationToken = default)
    {
        var errors = Validate(request);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var notFound = TypedResults.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Attachment not found",
            detail: $"No attachment exists with id {id}.",
            type: "https://tools.ietf.org/html/rfc7231#section-6.5.4");

        using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);

        var entity = await connection.GetAsync<AttachmentDto>((long)id);
        if (entity is null)
        {
            return notFound;
        }

        entity.title = request.Title;
        entity.description = request.Description;
        entity.s3_arn = request.S3Arn;
        entity.date_modified = DateTime.UtcNow;

        var updated = await connection.UpdateAsync(entity);
        if (!updated)
        {
            return notFound;
        }

        var response = new AttachmentResponse(
            entity.id,
            entity.title,
            entity.description,
            entity.s3_arn,
            entity.date_created,
            entity.date_modified);

        return TypedResults.Ok(response);
    }

    private static Dictionary<string, string[]> Validate(UpdateAttachmentRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.S3Arn))
        {
            errors[nameof(request.S3Arn)] = ["S3Arn is required."];
        }

        return errors;
    }

    // Request body accepted by this endpoint - owned by this slice, not shared.
    public sealed record UpdateAttachmentRequest(string? Title, string? Description, string S3Arn);

    // Shape returned to callers of this endpoint - owned by this slice, not shared.
    public sealed record AttachmentResponse(
        long Id,
        string? Title,
        string? Description,
        string S3Arn,
        DateTime DateCreated,
        DateTime? DateModified);
}
