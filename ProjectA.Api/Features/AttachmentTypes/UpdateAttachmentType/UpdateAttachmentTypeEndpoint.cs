using System.Text.RegularExpressions;
using Dapper.Contrib.Extensions;
using Microsoft.AspNetCore.Http.HttpResults;
using ProjectA.Api.Data;

namespace ProjectA.Api.Features.AttachmentTypes.UpdateAttachmentType;

public static partial class UpdateAttachmentTypeEndpoint
{
    public static void MapUpdateAttachmentType(this RouteGroupBuilder group)
    {
        group.MapPut("{id}", Handle)
            .WithName("UpdateAttachmentType")
            .WithSummary("Update an attachment type")
            .WithDescription(
                "Replaces an existing attachment type's title and color. Omit IconBase64 to leave " +
                "the current icon as-is, supply it to replace the icon, or set RemoveIcon to " +
                "clear it entirely.")
            .RequireAuthorization();
    }

    private static async Task<Results<Ok<AttachmentTypeResponse>, ValidationProblem, ProblemHttpResult>> Handle(
        uint id,
        UpdateAttachmentTypeRequest request,
        IDbConnectionFactory connectionFactory,
        CancellationToken cancellationToken = default)
    {
        var errors = Validate(request, out var newIcon, out var newIconContentType);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);

        // Icon replacement/removal needs the existing row first - omitting IconBase64 means
        // "leave the current icon as-is", which can't be expressed without knowing what it
        // currently is (same reasoning as UpdateBookmarkTypeEndpoint preserving the icon).
        var entity = await connection.GetAsync<AttachmentTypeDto>((long)id);
        if (entity is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Attachment type not found",
                detail: $"No attachment type exists with id {id}.",
                type: "https://tools.ietf.org/html/rfc7231#section-6.5.4");
        }

        entity.title = request.Title;
        entity.color = request.Color;

        if (request.RemoveIcon)
        {
            entity.icon = null;
            entity.icon_content_type = null;
        }
        else if (newIcon is not null)
        {
            entity.icon = newIcon;
            entity.icon_content_type = newIconContentType;
        }

        await connection.UpdateAsync(entity);

        var response = new AttachmentTypeResponse(entity.id, entity.title, entity.color, entity.icon is not null);

        return TypedResults.Ok(response);
    }

    private static Dictionary<string, string[]> Validate(
        UpdateAttachmentTypeRequest request, out byte[]? icon, out string? iconContentType)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            errors[nameof(request.Title)] = ["Title is required."];
        }

        if (!string.IsNullOrWhiteSpace(request.Color) && !ColorRegex().IsMatch(request.Color))
        {
            errors[nameof(request.Color)] = ["Color must be a hex value like #1A2B3C."];
        }

        if (!AttachmentTypeIcons.TryDecode(request.IconBase64, request.IconContentType, out icon, out iconContentType, out var iconError))
        {
            errors[nameof(request.IconBase64)] = [iconError!];
        }

        return errors;
    }

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
    private static partial Regex ColorRegex();

    // Request body accepted by this endpoint - owned by this slice, not shared.
    public sealed record UpdateAttachmentTypeRequest(
        string Title,
        string? Color,
        string? IconBase64,
        string? IconContentType,
        bool RemoveIcon);

    // Shape returned to callers of this endpoint - owned by this slice, not shared.
    public sealed record AttachmentTypeResponse(long Id, string Title, string? Color, bool HasIcon);
}
