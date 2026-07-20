using System.Text.RegularExpressions;
using Dapper.Contrib.Extensions;
using Microsoft.AspNetCore.Http.HttpResults;
using ProjectA.Api.Data;

namespace ProjectA.Api.Features.AttachmentTypes.CreateAttachmentType;

public static partial class CreateAttachmentTypeEndpoint
{
    public static void MapCreateAttachmentType(this RouteGroupBuilder group)
    {
        group.MapPost("", Handle)
            .WithName("CreateAttachmentType")
            .WithSummary("Create an attachment type")
            .WithDescription(
                "Creates a new attachment type, optionally with a small icon (as base64) and a " +
                "background color.")
            .RequireAuthorization();
    }

    private static async Task<Results<CreatedAtRoute<AttachmentTypeResponse>, ValidationProblem>> Handle(
        CreateAttachmentTypeRequest request,
        IDbConnectionFactory connectionFactory,
        CancellationToken cancellationToken = default)
    {
        var errors = Validate(request, out var icon, out var iconContentType);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var entity = new AttachmentTypeDto
        {
            title = request.Title,
            color = request.Color,
            icon = icon,
            icon_content_type = iconContentType
        };

        using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
        await connection.InsertAsync(entity);

        var response = new AttachmentTypeResponse(entity.id, entity.title, entity.color, entity.icon is not null);

        return TypedResults.CreatedAtRoute(response, "GetAttachmentTypeById", new { id = response.Id });
    }

    private static Dictionary<string, string[]> Validate(
        CreateAttachmentTypeRequest request, out byte[]? icon, out string? iconContentType)
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
    public sealed record CreateAttachmentTypeRequest(string Title, string? Color, string? IconBase64, string? IconContentType);

    // Shape returned to callers of this endpoint - owned by this slice, not shared. The icon
    // itself is never inlined into this response (it would bloat every list/get call) - callers
    // fetch it separately from GetAttachmentTypeIcon when HasIcon is true.
    public sealed record AttachmentTypeResponse(long Id, string Title, string? Color, bool HasIcon);
}
