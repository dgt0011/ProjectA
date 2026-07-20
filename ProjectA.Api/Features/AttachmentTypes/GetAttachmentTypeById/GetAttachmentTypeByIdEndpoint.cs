using Dapper.Contrib.Extensions;
using Microsoft.AspNetCore.Http.HttpResults;
using ProjectA.Api.Data;

namespace ProjectA.Api.Features.AttachmentTypes.GetAttachmentTypeById;

public static class GetAttachmentTypeByIdEndpoint
{
    public static void MapGetAttachmentTypeById(this RouteGroupBuilder group)
    {
        group.MapGet("{id}", Handle)
            .WithName("GetAttachmentTypeById")
            .WithSummary("Get an attachment type by Id")
            .WithDescription("Returns a single attachment type by Id.");
    }

    private static async Task<Results<Ok<AttachmentTypeResponse>, ProblemHttpResult>> Handle(
        uint id,
        IDbConnectionFactory connectionFactory,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
            var entity = await connection.GetAsync<AttachmentTypeDto>((long)id);

            if (entity is not null)
            {
                return TypedResults.Ok(new AttachmentTypeResponse(entity.id, entity.title, entity.color, entity.icon is not null));
            }
        }
        catch (Exception)
        {
            // TODO: Some logging is necessary
        }

        return TypedResults.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Attachment type not found",
            detail: $"No attachment type exists with id {id}.",
            type: "https://tools.ietf.org/html/rfc7231#section-6.5.4");
    }

    // Shape returned to callers of this endpoint - owned by this slice, not shared.
    public sealed record AttachmentTypeResponse(long Id, string Title, string? Color, bool HasIcon);
}
