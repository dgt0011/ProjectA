using System.Globalization;
using Markdig;

namespace ProjectA.Blog.Web.Common;

// Rendering helpers that reproduce Blogifier's output so pages look the same as before.
public static class BlogFormat
{
    public const string DefaultCover = "img/cover.jpg";

    // Blogifier's MdToHtml pipeline (pipe tables + advanced extensions). Post content is
    // authored only by signed-in admins and was already rendered unsanitised by Blogifier, so
    // raw HTML in Markdown is kept (existing posts rely on it).
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UsePipeTables()
        .UseAdvancedExtensions()
        .Build();

    public static string MarkdownToHtml(string? markdown) =>
        string.IsNullOrEmpty(markdown) ? string.Empty : Markdown.ToHtml(markdown, Pipeline);

    public static string MarkdownToPlainText(string? markdown) =>
        string.IsNullOrEmpty(markdown) ? string.Empty : Markdown.ToPlainText(markdown, Pipeline).Trim();

    // Blogifier's ToFriendlyShortDateString, e.g. "Dec 04, 2023".
    public static string FriendlyDate(DateTime value) =>
        value.ToString("MMM dd, yyyy", CultureInfo.InvariantCulture);

    // Stored cover/avatar values come in three forms: absolute URLs, site-relative paths
    // ("/data/2026/11/x.jpg") and Blogifier's root-relative paths without the leading slash
    // ("data/2/626.jpg", "img/cover.jpg"). Normalise to something an <img src> can use as-is.
    public static string AssetUrl(string? path, string? fallback = DefaultCover)
    {
        var value = string.IsNullOrWhiteSpace(path) ? fallback : path.Trim();
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
               || value.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
               || value.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
               || value.StartsWith('/')
            ? value
            : "/" + value;
    }

    public static string PostPath(short postType, string slug) =>
        postType == Models.PostType.Page ? $"/{slug}" : $"/posts/{slug}";
}
