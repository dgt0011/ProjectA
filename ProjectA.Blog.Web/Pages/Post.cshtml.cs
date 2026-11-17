using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProjectA.Blog.Web.Common;
using ProjectA.Blog.Web.Models;
using ProjectA.Blog.Web.Services;

namespace ProjectA.Blog.Web.Pages;

// Serves both /posts/{slug} (posts) and /{slug} (pages) - Blogifier's two URL shapes. A post
// requested at the page URL (or vice versa) is redirected to its canonical address.
public class PostModel(IBlogApiClient blogApi, ViewRecordingPolicy viewRecordingPolicy) : PageModel
{
    public PostDetail Detail { get; private set; } = new();
    public string ContentHtml { get; private set; } = string.Empty;
    public string DefaultCover { get; private set; } = BlogFormat.DefaultCover;
    public string AbsoluteUrl { get; private set; } = string.Empty;
    public string AbsoluteCoverUrl { get; private set; } = string.Empty;
    public string SiteTitle { get; private set; } = string.Empty;

    public bool IsPage => Detail.Post.PostType == PostType.Page;

    public async Task<IActionResult> OnGetAsync(string slug, CancellationToken cancellationToken)
    {
        var siteTask = blogApi.GetSiteAsync(cancellationToken);
        var result = await blogApi.GetPostAsync(slug, cancellationToken);
        if (!result.IsSuccess || result.Value is null)
        {
            return result.StatusCode == StatusCodes.Status404NotFound
                ? NotFound()
                : StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        Detail = result.Value;

        // This page is reachable at both /posts/{slug} and (via a convention in Program.cs)
        // /{slug}; which one was used decides whether a redirect is needed.
        var requestedAsPage = !Request.Path.StartsWithSegments("/posts");
        if (requestedAsPage != IsPage)
        {
            var canonicalPath = BlogFormat.PostPath(Detail.Post.PostType, Detail.Post.Slug);

            // A page whose slug is a reserved route ("posts") would redirect to itself forever.
            // The API now rejects those slugs; this guards against any that already exist.
            return string.Equals(canonicalPath, Request.Path.Value, StringComparison.OrdinalIgnoreCase)
                ? NotFound()
                : RedirectPermanent(canonicalPath);
        }

        // RemoteIpAddress is the reader's address: UseForwardedHeaders replaces nginx's with the
        // X-Forwarded-For value nginx appended.
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        if (viewRecordingPolicy.ShouldRecord(Request, ipAddress))
        {
            await blogApi.RecordViewAsync(slug, ipAddress, Request.Headers.UserAgent.ToString(), cancellationToken);
        }

        ContentHtml = BlogFormat.MarkdownToHtml(Detail.Content);
        var site = await siteTask;
        DefaultCover = site.Cover ?? BlogFormat.DefaultCover;
        SiteTitle = site.Title;
        var siteRoot = $"{Request.Scheme}://{Request.Host}{Request.PathBase}";
        AbsoluteUrl = siteRoot + BlogFormat.PostPath(Detail.Post.PostType, Detail.Post.Slug);
        var cover = BlogFormat.AssetUrl(Detail.Post.Cover, DefaultCover);
        AbsoluteCoverUrl = cover.StartsWith('/') ? siteRoot + cover : cover;
        return Page();
    }
}
