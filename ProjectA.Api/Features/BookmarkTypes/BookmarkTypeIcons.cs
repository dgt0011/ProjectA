namespace ProjectA.Api.Features.BookmarkTypes;

// Shared icon-decoding/validation for Create and Update - both accept the icon as a base64
// string plus its content type (rather than multipart/form-data, since these endpoints take a
// plain JSON body like every other slice in this API) and need to agree on the same rules.
internal static class BookmarkTypeIcons
{
    // Generous for what's meant to be a 16x16 icon/gif, but still a real guard against
    // someone accidentally wiring up a full-size photo upload here.
    private const int MaxIconBytes = 100_000;

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/png", "image/gif", "image/jpeg", "image/x-icon", "image/webp"
    };

    public static bool TryDecode(
        string? iconBase64,
        string? iconContentType,
        out byte[]? icon,
        out string? contentType,
        out string? error)
    {
        icon = null;
        contentType = null;
        error = null;

        if (string.IsNullOrWhiteSpace(iconBase64))
        {
            // No icon supplied - that's fine, a bookmark type's icon is optional.
            return true;
        }

        if (string.IsNullOrWhiteSpace(iconContentType) || !AllowedContentTypes.Contains(iconContentType))
        {
            error = "IconContentType must be one of: image/png, image/gif, image/jpeg, image/x-icon, image/webp.";
            return false;
        }

        try
        {
            icon = Convert.FromBase64String(iconBase64);
        }
        catch (FormatException)
        {
            error = "IconBase64 is not valid base64-encoded data.";
            return false;
        }

        if (icon.Length > MaxIconBytes)
        {
            error = $"Icon must be no larger than {MaxIconBytes / 1000} KB.";
            return false;
        }

        contentType = iconContentType;
        return true;
    }
}
